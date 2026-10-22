// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class ClipboardTransientLeaseTests
{
    private string directory = string.Empty;
    private string sourcePath = string.Empty;
    private TimeSpan originalTimeout;

    [TestInitialize]
    public void Initialize()
    {
        originalTimeout = Clipboard.TransientLeaseReleaseTimeout;
        Clipboard.LastDragDropFile = null;
        Clipboard.TransientLeaseReleaseTimeout = TimeSpan.FromMinutes(1);
        directory = Path.Combine(Environment.CurrentDirectory, $"clipboard-lease-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        sourcePath = Path.Combine(directory, "file.txt");
        File.WriteAllText(sourcePath, "content");
    }

    [TestCleanup]
    public void Cleanup()
    {
        Clipboard.LastDragDropFile = null;
        Clipboard.TransientLeaseReleaseTimeout = originalTimeout;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void CompletionWithoutReleaseRequestReleasesOwnerAfterLastTransfer()
    {
        using LocalPathLease owner = CreateLease();
        Publish(owner);
        Assert.IsTrue(Clipboard.TryAcquireLastDragDropFile(out _, out LocalPathLease first, out _, out _));
        Assert.IsNotNull(first);
        using (first)
        {
            Assert.IsTrue(Clipboard.TryAcquireLastDragDropFile(out _, out LocalPathLease second, out _, out _));
            Assert.IsNotNull(second);
            using (second)
            {
                Clipboard.CompleteLastDragDropFileSend(first);
                Assert.AreEqual(sourcePath, Clipboard.LastDragDropFile);
                Assert.IsFalse(owner.IsDisposed);

                Clipboard.CompleteLastDragDropFileSend(second);
                Assert.IsNull(Clipboard.LastDragDropFile);
                Assert.IsFalse(owner.IsDisposed);
            }
        }

        Assert.IsTrue(owner.IsDisposed);
    }

    [TestMethod]
    public void PublicationWithoutReleaseRequestExpiresWhenNotAcquired()
    {
        Clipboard.TransientLeaseReleaseTimeout = TimeSpan.FromMilliseconds(50);
        using LocalPathLease owner = CreateLease();
        Publish(owner);

        Assert.IsTrue(SpinWait.SpinUntil(() => owner.IsDisposed, TimeSpan.FromSeconds(5)));
        Assert.IsNull(Clipboard.LastDragDropFile);
    }

    [TestMethod]
    public void ExpiryWithoutReleaseRequestPreservesActiveTransferUntilCompletion()
    {
        using LocalPathLease owner = CreateLease();
        Publish(owner);
        Assert.IsTrue(Clipboard.TryAcquireLastDragDropFile(out _, out LocalPathLease acquired, out _, out _));
        Assert.IsNotNull(acquired);
        using (acquired)
        {
            object generation = typeof(Clipboard)
                .GetField("lastDragDropFileGeneration", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
            typeof(Clipboard)
                .GetMethod("ReleaseExpiredTransientDragFile", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new[] { generation });

            Assert.AreEqual(sourcePath, Clipboard.LastDragDropFile);
            Assert.IsFalse(owner.IsDisposed);

            Clipboard.CompleteLastDragDropFileSend(acquired);
            Assert.IsNull(Clipboard.LastDragDropFile);
            Assert.IsFalse(acquired.IsDisposed);
        }

        Assert.IsTrue(owner.IsDisposed);
    }

    private void Publish(LocalPathLease lease)
    {
        long generation = Clipboard.BeginTransientDragFileValidation();
        Assert.IsTrue(Clipboard.TrySetValidatedTransientDragFile(generation, sourcePath, lease));
    }

    private LocalPathLease CreateLease()
    {
        LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(sourcePath);
        Assert.IsNotNull(lease);
        return lease;
    }
}
