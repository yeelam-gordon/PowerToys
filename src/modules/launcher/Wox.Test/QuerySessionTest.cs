// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.ViewModel;

namespace Wox.Test
{
    [TestClass]
    public class QuerySessionTest
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan NegativeAssertionTimeout = TimeSpan.FromMilliseconds(250);

        [TestMethod]
        public void CancelSignalsCapturedTokenAfterSessionReplacement()
        {
            // Regression guard: an old query must retain its own canceled token after a newer query replaces it.
            using var firstSession = QuerySession.Start(_ => Task.CompletedTask);
            var firstToken = firstSession.Token;
            using var secondSession = QuerySession.Start(_ => Task.CompletedTask);

            firstSession.Cancel();

            Assert.IsTrue(firstToken.IsCancellationRequested);
            Assert.IsFalse(secondSession.Token.IsCancellationRequested);
        }

        [TestMethod]
        public void DisposeWhenCompleteDoesNotDisposeSourceWhileQueryRuns()
        {
            // A superseded query can still be inside plugin code, so its token source must remain usable until it exits.
            using var releaseQuery = new ManualResetEventSlim();
            CancellationToken capturedToken = default;
            var session = QuerySession.Start(token =>
            {
                capturedToken = token;
                return Task.Run(() => releaseQuery.Wait(WaitTimeout));
            });

            session.Cancel();
            var disposal = session.DisposeWhenComplete();
            var repeatedDisposal = session.DisposeWhenComplete();

            // Register throws after CTS disposal, so success proves the running query still owns a live source.
            using (capturedToken.Register(() => { }))
            {
                Assert.AreSame(disposal, repeatedDisposal);
                Assert.IsFalse(disposal.IsCompleted);
            }

            releaseQuery.Set();
            Assert.IsTrue(disposal.Wait(WaitTimeout));
        }

        [TestMethod]
        public void CancelAndWaitDefersDisposalWhenPluginIgnoresCancellation()
        {
            // Misbehaving plugins may outlive shutdown's wait budget; they must not observe a prematurely disposed source.
            using var releaseQuery = new ManualResetEventSlim();
            CancellationToken capturedToken = default;
            var session = QuerySession.Start(token =>
            {
                capturedToken = token;
                return Task.Run(() => releaseQuery.Wait(WaitTimeout));
            });

            Assert.IsFalse(session.CancelAndWait(NegativeAssertionTimeout));

            // Register throws after CTS disposal, so success proves timeout cleanup was safely deferred.
            using (capturedToken.Register(() => { }))
            {
            }

            releaseQuery.Set();
            Assert.IsTrue(session.Completion.Wait(WaitTimeout));
        }

        [TestMethod]
        public void DisposeIsSafeAfterPriorDisposalRequest()
        {
            // Query replacement and application shutdown can race, so repeated cleanup must remain harmless.
            var session = QuerySession.Start(_ => Task.CompletedTask);

            _ = session.DisposeWhenComplete();
            session.Dispose();
            session.Cancel();
        }

        [TestMethod]
        public void SuspendedSessionDoesNotRunPipelineBeforeResume()
        {
            // Query state must be published before its worker can produce results. Otherwise, a fast query can discard valid results as stale.
            var pipelineStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var session = QuerySession.StartSuspended(_ =>
            {
                pipelineStarted.SetResult(true);
                return Task.CompletedTask;
            });

            Assert.IsFalse(pipelineStarted.Task.Wait(NegativeAssertionTimeout));

            session.Resume();

            Assert.IsTrue(pipelineStarted.Task.Wait(WaitTimeout));
            Assert.IsTrue(session.Completion.Wait(WaitTimeout));
        }
    }
}
