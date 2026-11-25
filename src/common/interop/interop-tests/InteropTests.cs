// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.Interop;

namespace Microsoft.Interop.Tests
{
    [TestClass]
    public class InteropTests : IDisposable
    {
        // Pipe names are machine-global, so two concurrent test runs on the same CI agent
        // (or a leaked handle from a prior test) would deadlock if we used a shared constant.
        // Suffix with process id + a GUID so every test instance gets its own pair.
        private const string PipePrefix = @"\\.\pipe\";
        private readonly string pipeSuffix = $"{Environment.ProcessId}_{Guid.NewGuid():N}";

        private string ServerSidePipe => $"{PipePrefix}serverside_{pipeSuffix}";

        private string ClientSidePipe => $"{PipePrefix}clientside_{pipeSuffix}";

        private static readonly TimeSpan MessageWaitTimeout = TimeSpan.FromSeconds(30);

        internal TwoWayPipeMessageIPCManaged ClientPipe { get; set; }

        private bool disposedValue;

        [TestInitialize]
        public void Initialize()
        {
            ClientPipe = new TwoWayPipeMessageIPCManaged(ClientSidePipe, ServerSidePipe, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ClientPipe.End();
        }

        [TestMethod]
        public void TestSend()
        {
            var testString = "This string is a test\n";
            using (var reset = new AutoResetEvent(false))
            {
                using (var serverPipe = new TwoWayPipeMessageIPCManaged(
                    ServerSidePipe,
                    ClientSidePipe,
                    (string msg) =>
                    {
                        Assert.AreEqual(testString, msg);
                        reset.Set();
                    }))
                {
                    serverPipe.Start();
                    ClientPipe.Start();

                    // Test can be flaky as the pipes are still being set up and we end up receiving no message. Wait for a bit to avoid that.
                    Thread.Sleep(500);

                    ClientPipe.Send(testString);

                    // Bounded wait so a broken pipe handshake fails the test quickly
                    // instead of hanging the CI agent until the job-level timeout.
                    var timeoutMessage = $"Pipe callback was not invoked within {MessageWaitTimeout.TotalSeconds}s. Server='{ServerSidePipe}' Client='{ClientSidePipe}'.";
                    Assert.IsTrue(reset.WaitOne(MessageWaitTimeout), timeoutMessage);

                    serverPipe.End();
                }
            }
        }

#if DEBUG
        [TestMethod]
        public void PeerBoundStartExchangesMessagesWithTheExpectedOwner()
        {
            using var received = new AutoResetEvent(false);
            using var reply = new AutoResetEvent(false);
            using var server = new TwoWayPipeMessageIPCManaged(
                ServerSidePipe,
                ClientSidePipe,
                message =>
                {
                    if (message == "request")
                    {
                        received.Set();
                    }
                });
            using var client = new TwoWayPipeMessageIPCManaged(
                ClientSidePipe,
                ServerSidePipe,
                message =>
                {
                    if (message == "reply")
                    {
                        reply.Set();
                    }
                });
            var startOwner = Task.Run(() =>
            {
                Thread.Sleep(100);
                server.Start();
            });
            try
            {
                var executable = Environment.ProcessPath;
                client.StartWithPeer((uint)Environment.ProcessId, Path.GetDirectoryName(executable), new[] { Path.GetFileName(executable) });
                using (var probe = new NamedPipeClientStream(".", ClientSidePipe[PipePrefix.Length..], PipeDirection.InOut))
                {
                    probe.Connect(5000);
                }

                client.Send("request");
                Assert.IsTrue(received.WaitOne(MessageWaitTimeout));
                server.Send("reply");
                Assert.IsTrue(reply.WaitOne(MessageWaitTimeout));
            }
            finally
            {
                startOwner.GetAwaiter().GetResult();
                client.End();
                server.End();
            }
        }

        [TestMethod]
        public void PeerBoundStartRejectsAnExistingEndpointOutsideTheOwnerImagePolicy()
        {
            using var received = new AutoResetEvent(false);
            using var server = new TwoWayPipeMessageIPCManaged(ServerSidePipe, ClientSidePipe, _ => received.Set());
            server.Start();
            try
            {
                Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
                    ClientPipe.StartWithPeer(
                        (uint)Environment.ProcessId,
                        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                        new[] { Path.GetFileName(Environment.ProcessPath) }));
                Assert.IsFalse(received.WaitOne(0), "An identity probe must not dispatch an application message.");
            }
            finally
            {
                server.End();
            }
        }

        [TestMethod]
        public void PeerBoundReceiverRejectsAnotherProcessBeforeDispatchingWarningContents()
        {
            var requestId = Guid.NewGuid().ToString("B");
            var expected = $"{{\"type\":\"elevation-warning\",\"requestId\":\"{requestId}\",\"appName\":\"expected\"}}";
            var different = $"{{\"type\":\"elevation-warning\",\"requestId\":\"{requestId}\",\"appName\":\"different\"}}";
            using var received = new AutoResetEvent(false);
            using var unexpected = new AutoResetEvent(false);
            using var server = new TwoWayPipeMessageIPCManaged(ServerSidePipe, ClientSidePipe, null);
            using var client = new TwoWayPipeMessageIPCManaged(
                ClientSidePipe,
                ServerSidePipe,
                message =>
                {
                    if (message == expected)
                    {
                        received.Set();
                    }
                    else
                    {
                        unexpected.Set();
                    }
                });
            server.Start();
            try
            {
                var executable = Environment.ProcessPath;
                client.StartWithPeer((uint)Environment.ProcessId, Path.GetDirectoryName(executable), new[] { Path.GetFileName(executable) });
                using (var probe = new NamedPipeClientStream(".", ClientSidePipe[PipePrefix.Length..], PipeDirection.InOut))
                {
                    probe.Connect(5000);
                }

                var payload = Convert.ToBase64String(Encoding.Unicode.GetBytes(different));
                var script = $"$ErrorActionPreference='Stop'; $p=[IO.Pipes.NamedPipeClientStream]::new('.', '{ClientSidePipe[PipePrefix.Length..]}', [IO.Pipes.PipeDirection]::InOut); " +
                    $"try {{ $p.Connect(5000); Write-Output 'connected'; $bytes=[Convert]::FromBase64String('{payload}'); " +
                    "try { $p.Write($bytes,0,$bytes.Length) } catch [IO.IOException] { Write-Output 'peer closed' } } finally { $p.Dispose() }";
                var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                start.ArgumentList.Add("-NoProfile");
                start.ArgumentList.Add("-NonInteractive");
                start.ArgumentList.Add("-EncodedCommand");
                start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
                using var peer = Process.Start(start);
                Assert.IsNotNull(peer);
                try
                {
                    Assert.IsTrue(peer.WaitForExit(10000), "The owned pipe peer did not exit.");
                    Assert.AreEqual(0, peer.ExitCode, peer.StandardError.ReadToEnd());
                    StringAssert.Contains(peer.StandardOutput.ReadToEnd(), "connected");
                }
                finally
                {
                    if (!peer.HasExited)
                    {
                        peer.Kill(entireProcessTree: true);
                        peer.WaitForExit();
                    }
                }

                server.Send(expected);
                Assert.IsTrue(received.WaitOne(MessageWaitTimeout), "The intended owner must still deliver its warning.");
                Assert.IsFalse(unexpected.WaitOne(1000), "Another process must not supply warning contents, even with the current request ID.");
            }
            finally
            {
                client.End();
                server.End();
            }
        }
#endif

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ClientPipe.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
