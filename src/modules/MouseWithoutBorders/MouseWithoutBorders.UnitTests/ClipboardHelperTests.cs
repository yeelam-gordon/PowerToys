// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class ClipboardHelperTests
{
    private static readonly int[] ExpectedQueuedInputOrder = { 1, 0x0202, 0x0101, 0x0201 };

    [DataTestMethod]
    [DataRow(@"\\attacker\share\file.txt")]
    [DataRow(@"//attacker/share/file.txt")]
    [DataRow(@"\\?\UNC\attacker\share\file.txt")]
    [DataRow(@"\\.\GLOBALROOT\Device\Mup\attacker\share\file.txt")]
    public void LocalPathLease_RejectsRemoteOrDevicePath(string path)
    {
        using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);

        Assert.IsNull(lease);
    }

    [TestMethod]
    public void LocalPathLease_RejectsMissingPath()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.txt");
        using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);

        Assert.IsNull(lease);
    }

    [DataTestMethod]
    [DataRow(@"\Device\HarddiskVolume3\Users")]
    [DataRow(@"\Device\HarddiskVolume3\mount\redirect")]
    [DataRow(@"\??\C:\Users")]
    [DataRow(@"\Device\Mup")]
    [DataRow(@"\Device\LanmanRedirector")]
    [DataRow(@"\Device\WebDavRedirector")]
    public void TryGetLocalDevicePath_RejectsNonVolumeRootTargets(string target)
    {
        Assert.IsFalse(LocalPathLease.TryGetLocalDevicePath(
            @"C:\source\file.txt",
            _ => DriveType.Fixed,
            _ => target,
            out _,
            out _,
            out _));
    }

    [TestMethod]
    public void TryGetLocalDevicePath_AcceptsDirectLocalVolumeRoot()
    {
        Assert.IsTrue(LocalPathLease.TryGetLocalDevicePath(
            @"C:\source\file.txt",
            _ => DriveType.Fixed,
            _ => @"\Device\HarddiskVolume3",
            out string displayPath,
            out string deviceRoot,
            out string physicalPath));

        Assert.AreEqual(@"C:\source\file.txt", displayPath);
        Assert.AreEqual(@"\\?\GLOBALROOT\Device\HarddiskVolume3", deviceRoot);
        Assert.AreEqual(@"\\?\GLOBALROOT\Device\HarddiskVolume3\source\file.txt", physicalPath);
    }

    [TestMethod]
    public void LocalPathLease_RejectsPathTraversingSymbolicLink()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string targetDirectory = Path.Combine(directory, "target");
        string linkDirectory = Path.Combine(directory, "link");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, "file.txt"), "content");

        try
        {
            try
            {
                Directory.CreateSymbolicLink(linkDirectory, targetDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Assert.Inconclusive($"Creating a symbolic link is unavailable: {exception.Message}");
            }

            using LocalPathLease lease =
                LocalPathLease.TryCreateForCurrentUser(Path.Combine(linkDirectory, "file.txt"));

            Assert.IsNull(lease);
        }
        finally
        {
            if (Directory.Exists(linkDirectory))
            {
                Directory.Delete(linkDirectory);
            }

            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LocalPathLease_OpensExistingLocalFile()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        string movedPath = Path.Combine(sourceDirectory, "moved.txt");
        File.WriteAllText(path, "content");

        try
        {
            using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);

            Assert.IsNotNull(lease);
            Assert.IsFalse(lease.IsDirectory);
            Assert.AreEqual(new FileInfo(path).Length, lease.Length);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LocalPathLease_AcquiredReferenceKeepsFinalHandleAlive()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        File.WriteAllText(path, "content");

        LocalPathLease? lease = null;
        LocalPathLease? acquiredLease = null;
        try
        {
            lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);
            acquiredLease = lease.Acquire();
            Assert.IsNotNull(acquiredLease);

            lease.Dispose();
            lease = null;
            Assert.IsFalse(acquiredLease.IsDisposed);

            acquiredLease.Dispose();
            Assert.IsTrue(acquiredLease.IsDisposed);
            acquiredLease = null;
        }
        finally
        {
            acquiredLease?.Dispose();
            lease?.Dispose();
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LocalPathLease_TransferStreamTemporarilyPreventsMutation()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        string movedPath = Path.Combine(sourceDirectory, "moved.txt");
        File.WriteAllText(path, "content");

        try
        {
            using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);

            Assert.IsTrue(TryOpenForWrite(path));
            using (FileStream stream = lease.OpenReadStream(4096))
            {
                Assert.IsNotNull(stream);
                Assert.IsFalse(TryOpenForWrite(path));
                using StreamReader reader = new(stream);
                Assert.AreEqual("content", reader.ReadToEnd());
            }

            Assert.IsTrue(TryOpenForWrite(path));
            File.Move(path, movedPath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LocalPathLease_FinalHandleSurvivesPathReplacement()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        string movedPath = Path.Combine(sourceDirectory, "moved.txt");
        File.WriteAllText(path, "original");

        try
        {
            using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);

            File.Move(path, movedPath);
            File.WriteAllText(path, "replacement");

            using FileStream stream = lease.OpenReadStream(4096);
            Assert.IsNotNull(stream);
            using StreamReader reader = new(stream);
            Assert.AreEqual("original", reader.ReadToEnd());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LocalPathLease_OpenReadStreamOwnsIndependentFinalHandles()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(directory, "file.txt");
        File.WriteAllText(path, "content");

        LocalPathLease? lease = null;
        FileStream? firstStream = null;
        FileStream? secondStream = null;
        try
        {
            lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);

            firstStream = lease.OpenReadStream(4096);
            secondStream = lease.OpenReadStream(4096);
            Assert.IsNotNull(firstStream);
            Assert.IsNotNull(secondStream);

            lease.Dispose();
            lease = null;

            Assert.AreEqual((int)'c', firstStream.ReadByte());
            Assert.AreEqual((int)'c', secondStream.ReadByte());
            Assert.IsFalse(TryOpenForWrite(path));

            firstStream.Dispose();
            firstStream = null;
            secondStream.Dispose();
            secondStream = null;
            Assert.IsTrue(TryOpenForWrite(path));
        }
        finally
        {
            firstStream?.Dispose();
            secondStream?.Dispose();
            lease?.Dispose();
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Clipboard_DirectoryStatusDoesNotRetainLease()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string movedDirectory = directory + "-moved";

        try
        {
            using (LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(directory))
            {
                Assert.IsNotNull(lease);
                Assert.IsTrue(lease.IsDirectory);
            }

            Clipboard.SetLastDragDropFile(directory, null, isDirectory: true);

            Assert.IsTrue(Clipboard.TryAcquireLastDragDropFile(
                out string storedPath,
                out LocalPathLease storedLease,
                out bool isDirectory,
                out bool requiresLease));
            Assert.AreEqual(directory, storedPath);
            Assert.IsNull(storedLease);
            Assert.IsTrue(isDirectory);
            Assert.IsFalse(requiresLease);

            Directory.Move(directory, movedDirectory);
        }
        finally
        {
            Clipboard.LastDragDropFile = null;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            if (Directory.Exists(movedDirectory))
            {
                Directory.Delete(movedDirectory, true);
            }
        }
    }

    [TestMethod]
    public void Clipboard_FileStateRevalidatesWithoutRetainingHandles()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        string movedDirectory = Path.Combine(directory, "moved");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        File.WriteAllText(path, "content");

        try
        {
            Clipboard.SetLastDragDropFile(path, null, requiresLease: true);

            Assert.IsTrue(Clipboard.TryAcquireLastDragDropFile(
                out string storedPath,
                out LocalPathLease storedLease,
                out bool isDirectory,
                out bool requiresLease));
            Assert.AreEqual(path, storedPath);
            Assert.IsNull(storedLease);
            Assert.IsFalse(isDirectory);
            Assert.IsTrue(requiresLease);

            Directory.Move(sourceDirectory, movedDirectory);
        }
        finally
        {
            Clipboard.LastDragDropFile = null;
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Clipboard_TransientLeaseStaysAliveDuringTransferAndReleasesAfterCompletion()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        string movedDirectory = Path.Combine(directory, "moved");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        File.WriteAllText(path, "content");

        TimeSpan originalTimeout = Clipboard.TransientLeaseReleaseTimeout;
        LocalPathLease? acquiredLease = null;
        LocalPathLease? leaseReference = null;
        try
        {
            Clipboard.TransientLeaseReleaseTimeout = TimeSpan.FromMilliseconds(50);
            LocalPathLease? lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);
            leaseReference = lease;
            long validationGeneration = Clipboard.BeginTransientDragFileValidation();
            Assert.IsTrue(Clipboard.TrySetValidatedTransientDragFile(validationGeneration, path, lease));
            Clipboard.RequestLastDragDropFileReleaseAfterSend();

            Assert.IsTrue(Clipboard.TryAcquireLastDragDropFile(
                out string storedPath,
                out acquiredLease,
                out bool isDirectory,
                out bool requiresLease));
            Assert.AreEqual(path, storedPath);
            Assert.IsNotNull(acquiredLease);
            Assert.IsFalse(isDirectory);
            Assert.IsFalse(requiresLease);

            System.Threading.Thread.Sleep(200);
            Assert.AreEqual(path, Clipboard.LastDragDropFile);
            Assert.IsFalse(leaseReference.IsDisposed);

            LocalPathLease completedLease = acquiredLease;
            Clipboard.CompleteLastDragDropFileSend(completedLease);

            Assert.IsNull(Clipboard.LastDragDropFile);
            Assert.IsFalse(leaseReference.IsDisposed);

            acquiredLease.Dispose();
            acquiredLease = null;
            Assert.IsTrue(leaseReference.IsDisposed);
            Directory.Move(sourceDirectory, movedDirectory);
        }
        finally
        {
            acquiredLease?.Dispose();
            Clipboard.LastDragDropFile = null;
            Clipboard.TransientLeaseReleaseTimeout = originalTimeout;
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Clipboard_TransientLeaseExpiresWhenNoDestinationAcquiresIt()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(directory, "file.txt");
        File.WriteAllText(path, "content");

        TimeSpan originalTimeout = Clipboard.TransientLeaseReleaseTimeout;
        LocalPathLease? leaseReference = null;
        try
        {
            Clipboard.TransientLeaseReleaseTimeout = TimeSpan.FromMilliseconds(50);
            LocalPathLease? lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);
            leaseReference = lease;
            long validationGeneration = Clipboard.BeginTransientDragFileValidation();
            Assert.IsTrue(Clipboard.TrySetValidatedTransientDragFile(validationGeneration, path, lease));
            Clipboard.RequestLastDragDropFileReleaseAfterSend();

            Assert.IsFalse(leaseReference.IsDisposed);
            Assert.IsTrue(SpinWait.SpinUntil(() => leaseReference.IsDisposed, TimeSpan.FromSeconds(5)));
            Assert.IsNull(Clipboard.LastDragDropFile);
        }
        finally
        {
            Clipboard.LastDragDropFile = null;
            Clipboard.TransientLeaseReleaseTimeout = originalTimeout;
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Clipboard_RepeatedReleaseRequestsDoNotExtendTransientTimeout()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(directory, "file.txt");
        File.WriteAllText(path, "content");

        TimeSpan originalTimeout = Clipboard.TransientLeaseReleaseTimeout;
        LocalPathLease? leaseReference = null;
        try
        {
            Clipboard.TransientLeaseReleaseTimeout = TimeSpan.FromMilliseconds(500);
            LocalPathLease? lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);
            leaseReference = lease;
            long validationGeneration = Clipboard.BeginTransientDragFileValidation();
            Assert.IsTrue(Clipboard.TrySetValidatedTransientDragFile(validationGeneration, path, lease));
            Clipboard.RequestLastDragDropFileReleaseAfterSend();

            System.Threading.Thread.Sleep(350);
            Clipboard.RequestLastDragDropFileReleaseAfterSend();

            Assert.IsTrue(SpinWait.SpinUntil(() => leaseReference.IsDisposed, TimeSpan.FromMilliseconds(300)));
            Assert.IsNull(Clipboard.LastDragDropFile);
        }
        finally
        {
            Clipboard.LastDragDropFile = null;
            Clipboard.TransientLeaseReleaseTimeout = originalTimeout;
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Clipboard_TransientValidationDoesNotPublishAfterButtonUp()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(directory, "file.txt");
        File.WriteAllText(path, "content");

        LocalPathLease? lease = null;
        try
        {
            long validationGeneration = Clipboard.BeginTransientDragFileValidation();
            Clipboard.RequestLastDragDropFileReleaseAfterSend();

            lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);
            Assert.IsFalse(Clipboard.TrySetValidatedTransientDragFile(validationGeneration, path, lease));
            Assert.IsNull(Clipboard.LastDragDropFile);

            lease.Dispose();
            lease = null;
            Assert.IsTrue(TryOpenForWrite(path));
        }
        finally
        {
            lease?.Dispose();
            Clipboard.LastDragDropFile = null;
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Clipboard_StaleValidationCannotClaimNewGesture()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(directory, "file.txt");
        File.WriteAllText(path, "content");

        LocalPathLease? lease = null;
        try
        {
            long staleGeneration = Clipboard.BeginTransientDragFileValidation();
            long currentGeneration = Clipboard.BeginTransientDragFileValidation();
            lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);

            Assert.IsFalse(Clipboard.TrySetValidatedTransientDragFile(staleGeneration, path, lease));
            Assert.IsNull(Clipboard.LastDragDropFile);
            Assert.IsTrue(Clipboard.TrySetValidatedTransientDragFile(currentGeneration, path, lease));
            lease = null;
            Assert.AreEqual(path, Clipboard.LastDragDropFile);
        }
        finally
        {
            lease?.Dispose();
            Clipboard.LastDragDropFile = null;
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Clipboard_CancellingTransientLeaseReleasesImmediately()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(directory, "file.txt");
        File.WriteAllText(path, "content");

        try
        {
            LocalPathLease? lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);
            Clipboard.SetLastDragDropFile(path, lease, isTransient: true);
            Assert.IsFalse(lease.IsDisposed);

            Clipboard.LastDragDropFile = null;

            Assert.IsTrue(lease.IsDisposed);
        }
        finally
        {
            Clipboard.LastDragDropFile = null;
            Directory.Delete(directory, true);
        }
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DragDrop_ObsoleteValidationDoesNotCompleteNewGesture(bool hasValidatedPath)
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(directory, "file.txt");
        File.WriteAllText(path, "content");
        LocalPathLease? lease = null;
        bool originalMouseDown = DragDrop.MouseDown;
        try
        {
            long obsoleteGeneration = DragDrop.BeginDragGesture();
            if (hasValidatedPath)
            {
                lease = LocalPathLease.TryCreateForCurrentUser(path);
                Assert.IsNotNull(lease);
            }

            long currentGeneration = DragDrop.BeginDragGesture();
            Assert.IsFalse(DragDrop.TryCompleteDragFileValidation(obsoleteGeneration, path, lease, out bool activated));
            Assert.IsFalse(activated);
            Assert.IsTrue(DragDrop.MouseDown);
            Assert.IsFalse(DragDrop.IsCurrentDragGeneration(obsoleteGeneration));
            Assert.IsTrue(DragDrop.IsCurrentDragGeneration(currentGeneration));

            lease ??= LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);
            Assert.IsTrue(Clipboard.TrySetValidatedTransientDragFile(currentGeneration, path, lease));
            lease = null;
        }
        finally
        {
            lease?.Dispose();
            Clipboard.LastDragDropFile = null;
            DragDrop.MouseDown = originalMouseDown;
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void DragDrop_OrderedNotificationsDoNotBlockButtonUp()
    {
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim release = new(false);
        using ManualResetEventSlim secondSent = new(false);
        bool firstTimedOut = false;
        Task first = DragDrop.QueueDragNotification(() =>
        {
            entered.Set();
            firstTimedOut = !release.Wait(TimeSpan.FromSeconds(10));
        });
        Task? second = null;
        Task? buttonUp = null;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            second = DragDrop.QueueDragNotification(secondSent.Set);
            buttonUp = Task.Run(() => DragDrop.DragDropStep09(0x0202));
            Assert.IsTrue(buttonUp.Wait(TimeSpan.FromSeconds(2)), "Button-up must not wait for a notification write.");
            Assert.IsFalse(secondSent.IsSet, "Notifications must retain their enqueue order.");
        }
        finally
        {
            release.Set();
            Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)));
            if (second != null)
            {
                Assert.IsTrue(second.Wait(TimeSpan.FromSeconds(5)));
            }

            if (buttonUp != null)
            {
                Assert.IsTrue(buttonUp.Wait(TimeSpan.FromSeconds(5)));
            }
        }

        Assert.IsFalse(firstTimedOut);
        Assert.IsTrue(secondSent.IsSet);
    }

    [TestMethod]
    public void DragDrop_ObsoleteCallbackCannotStopNewPositioning()
    {
        bool originalMouseDown = DragDrop.MouseDown;
        try
        {
            long obsoleteGeneration = DragDrop.BeginDragGesture();
            long currentGeneration = DragDrop.BeginDragGesture();
            Assert.IsFalse(DragDrop.TryRecordDragValidationCallback(obsoleteGeneration));
            Assert.IsFalse(DragDrop.HasDragValidationCallback(currentGeneration));
            Assert.IsTrue(DragDrop.TryRecordDragValidationCallback(currentGeneration));
            Assert.IsFalse(DragDrop.TryRecordDragValidationCallback(obsoleteGeneration));
            Assert.IsTrue(DragDrop.HasDragValidationCallback(currentGeneration));
        }
        finally
        {
            Clipboard.LastDragDropFile = null;
            DragDrop.MouseDown = originalMouseDown;
        }
    }

    [TestMethod]
    public void DragDrop_PendingActivationPreservesInputOrderAndSnapshots()
    {
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim release = new(false);
        List<int> sent = new();
        bool firstTimedOut = false;
        Task first = DragDrop.QueueDragNotification(() =>
        {
            entered.Set();
            firstTimedOut = !release.Wait(TimeSpan.FromSeconds(10));
        });
        Task? drained = null;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            _ = DragDrop.QueueDragNotification(() => sent.Add(1));
            DATA mouse = new() { Type = PackageType.Mouse, Des = (ID)7 };
            mouse.Md.dwFlags = 0x0202;
            Assert.IsTrue(DragDrop.TryQueueInputPacket(mouse, packet => sent.Add(packet.Md.dwFlags)));
            DATA keyboard = new() { Type = PackageType.Keyboard, Des = (ID)7 };
            keyboard.Kd.dwFlags = 0x0101;
            Assert.IsTrue(DragDrop.TryQueueInputPacket(keyboard, packet => sent.Add(packet.Kd.dwFlags)));
            keyboard.Kd.dwFlags = 0;
            mouse.Md.dwFlags = 0x0201;
            Assert.IsTrue(DragDrop.TryQueueInputPacket(mouse, packet => sent.Add(packet.Md.dwFlags)));
            mouse.Md.dwFlags = 0;
            drained = DragDrop.QueueDragNotification(() => { });
            Assert.AreEqual(0, sent.Count);
        }
        finally
        {
            release.Set();
            Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)));
            if (drained != null)
            {
                Assert.IsTrue(drained.Wait(TimeSpan.FromSeconds(5)));
            }
        }

        Assert.IsFalse(firstTimedOut);
        CollectionAssert.AreEqual(ExpectedQueuedInputOrder, sent);
    }

    [TestMethod]
    public void DragDrop_MachineSwitchCleanupFollowsPendingModifierAndCapturesOldDestination()
    {
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim release = new(false);
        bool firstTimedOut = false;
        bool oldModifierDown = false;
        bool cleanupFollowedModifier = false;
        ID cleanupDestination = ID.NONE;
        PackageType cleanupType = default;
        bool cleanupResetDestinationOnFailure = true;
        ID keyUpDestination = ID.NONE;
        Task first = DragDrop.QueueDragNotification(() =>
        {
            entered.Set();
            firstTimedOut = !release.Wait(TimeSpan.FromSeconds(10));
        });
        Task? drained = null;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            ID destination = (ID)7;
            DATA keyDown = new() { Type = PackageType.Keyboard, Des = destination };
            keyDown.Kd.wVk = 0x11;
            keyDown.Kd.dwFlags = 0x0100;
            Assert.IsTrue(DragDrop.TryQueueInputPacket(keyDown, packet => oldModifierDown = packet.Des == (ID)7));

            Task cleanup = DragDrop.QueueMachineSwitchCleanup(destination, (oldDestination, type, resetDestinationOnFailure) =>
            {
                cleanupFollowedModifier = oldModifierDown;
                cleanupDestination = oldDestination;
                cleanupType = type;
                oldModifierDown = false;
                cleanupResetDestinationOnFailure = resetDestinationOnFailure;
            });
            Assert.IsFalse(cleanup.IsCompleted, "Switch cleanup must queue without waiting for the blocked send.");

            destination = (ID)8;
            DATA keyUp = new() { Type = PackageType.Keyboard, Des = destination };
            keyUp.Kd.wVk = 0x11;
            keyUp.Kd.dwFlags = 0x0101;
            Assert.IsTrue(DragDrop.TryQueueInputPacket(keyUp, packet => keyUpDestination = packet.Des));
            drained = DragDrop.QueueDragNotification(() => { });
        }
        finally
        {
            release.Set();
            Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)));
            if (drained != null)
            {
                Assert.IsTrue(drained.Wait(TimeSpan.FromSeconds(5)));
            }
        }

        Assert.IsFalse(firstTimedOut);
        Assert.IsTrue(cleanupFollowedModifier);
        Assert.IsFalse(oldModifierDown);
        Assert.AreEqual((ID)7, cleanupDestination);
        Assert.AreEqual(PackageType.HideMouse, cleanupType);
        Assert.IsFalse(cleanupResetDestinationOnFailure);
        Assert.AreEqual((ID)8, keyUpDestination);
    }

    [TestMethod]
    public void DragDrop_DeferredCleanupFailurePreservesNewDestination()
    {
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim release = new(false);
        bool firstTimedOut = false;
        ID destination = (ID)7;
        Task first = DragDrop.QueueDragNotification(() =>
        {
            entered.Set();
            firstTimedOut = !release.Wait(TimeSpan.FromSeconds(10));
        });
        Task? cleanup = null;
        bool failureAttempted = false;
        ID failedDestination = ID.NONE;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            cleanup = DragDrop.QueueMachineSwitchCleanup(destination, (oldDestination, _, resetDestinationOnFailure) =>
            {
                failureAttempted = true;
                failedDestination = oldDestination;
                if (resetDestinationOnFailure)
                {
                    destination = (ID)1;
                }
            });
            destination = (ID)8;
        }
        finally
        {
            release.Set();
            Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)));
            if (cleanup != null)
            {
                Assert.IsTrue(cleanup.Wait(TimeSpan.FromSeconds(5)));
            }
        }

        Assert.IsFalse(firstTimedOut);
        Assert.IsTrue(failureAttempted);
        Assert.AreEqual((ID)7, failedDestination);
        Assert.AreEqual((ID)8, destination);
    }

    [TestMethod]
    public void DragDrop_ExplorerCheckFollowsPendingButtonDownAndCapturesRoute()
    {
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim release = new(false);
        string originalMachineName = Common.MachineName;
        bool firstTimedOut = false;
        bool sourceMouseDown = false;
        bool checkSawMouseDown = false;
        DATA? sentCheck = null;
        Task first = DragDrop.QueueDragNotification(() =>
        {
            entered.Set();
            firstTimedOut = !release.Wait(TimeSpan.FromSeconds(10));
        });
        Task? check = null;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            DATA buttonDown = new() { Type = PackageType.Mouse, Des = (ID)7 };
            buttonDown.Md.dwFlags = 0x0201;
            Assert.IsTrue(DragDrop.TryQueueInputPacket(buttonDown, packet => sourceMouseDown = packet.Md.dwFlags == 0x0201));
            ID source = (ID)8;
            ID destination = (ID)7;
            Common.MachineName = "FixtureHost";
            check = DragDrop.QueueExplorerDragDropCheck(source, destination, packet =>
            {
                checkSawMouseDown = sourceMouseDown;
                sentCheck = packet;
            });
            Common.MachineName = "NextHost";
            Assert.IsFalse(check.IsCompleted, "The check must queue without waiting for pending input.");
        }
        finally
        {
            release.Set();
            Assert.IsTrue(first.Wait(TimeSpan.FromSeconds(5)));
            if (check != null)
            {
                Assert.IsTrue(check.Wait(TimeSpan.FromSeconds(5)));
            }

            Common.MachineName = originalMachineName;
        }

        Assert.IsFalse(firstTimedOut);
        Assert.IsTrue(checkSawMouseDown);
        Assert.IsNotNull(sentCheck);
        Assert.AreEqual(PackageType.ExplorerDragDrop, sentCheck.Type);
        Assert.AreEqual((ID)8, sentCheck.Src);
        Assert.AreEqual((ID)7, sentCheck.Des);
        Assert.AreEqual("FixtureHost", sentCheck.MachineName);
    }

    [TestMethod]
    public void HelperCallback_ReentrantDataRetrievalCannotConsumeNewGeneration()
    {
        long pendingGeneration = 1;
        string[] files = new string[1];
        files[0] = "old.txt";
        Assert.IsFalse(DragFileCallback.TryReadFile(
            ref pendingGeneration,
            () =>
            {
                _ = Interlocked.Exchange(ref pendingGeneration, 2);
                return files;
            },
            out string? oldFile,
            out long oldGeneration));
        Assert.IsNull(oldFile);
        Assert.AreEqual(1L, oldGeneration);
        Assert.AreEqual(2L, pendingGeneration);

        files[0] = "current.txt";
        Assert.IsTrue(DragFileCallback.TryReadFile(ref pendingGeneration, () => files, out string? currentFile, out long currentGeneration));
        Assert.AreEqual("current.txt", currentFile);
        Assert.AreEqual(2L, currentGeneration);
        Assert.AreEqual(0L, pendingGeneration);
    }

    private static bool TryMoveDirectory(string sourceDirectory, string destinationDirectory)
    {
        try
        {
            Directory.Move(sourceDirectory, destinationDirectory);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryOpenForWrite(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
