// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class LocalPathLeaseValidationTests
{
    [TestMethod]
    public void ValidationDirectory_PreventsReplacementUntilReleased()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, $"LocalPathLease-{Guid.NewGuid():N}");
        string parent = Path.Combine(directory, "parent");
        string movedParent = Path.Combine(directory, "moved-parent");
        Directory.CreateDirectory(parent);
        string path = Path.Combine(parent, "file.txt");
        File.WriteAllText(path, "original");

        try
        {
            using (var handle = LocalPathLease.OpenValidationDirectory(parent))
            {
                Assert.IsNotNull(handle);
                IOException exception = Assert.ThrowsExactly<IOException>(() => Directory.Move(parent, movedParent));
                Assert.AreEqual(32, exception.HResult & 0xffff, "Renaming a checked parent must fail with ERROR_SHARING_VIOLATION.");
                Assert.IsFalse(Directory.Exists(movedParent));

                using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);
                Assert.IsNotNull(lease);
                using FileStream stream = lease.OpenReadStream(4096);
                Assert.IsNotNull(stream);
                using StreamReader reader = new(stream);
                Assert.AreEqual("original", reader.ReadToEnd());
            }

            Directory.Move(parent, movedParent);
            Directory.CreateDirectory(parent);
            File.WriteAllText(path, "replacement");
            Assert.AreEqual("original", File.ReadAllText(Path.Combine(movedParent, "file.txt")));
            Assert.AreEqual("replacement", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
