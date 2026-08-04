// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

// Guards the fix for MSRC 110760 / ICM 31000000569630: the ClipboardHelper IPC
// endpoint must reject UNC/remote and reparse-point paths so a malicious pipe client
// cannot coerce outbound SMB authentication by injecting a path that is then probed
// via File.Exists/Directory.Exists.
[TestClass]
public sealed class ClipboardHelperTests
{
    [DataTestMethod]
    [DataRow(@"\\attacker\share\file.txt")]
    [DataRow(@"\\10.0.0.1\share")]
    [DataRow(@"\\server")]
    [DataRow(@"//attacker/share/file.txt")]
    [DataRow(@"\\?\UNC\server\share\file.txt")]
    [DataRow(@"\\.\pipe\evil")]
    public void IsRemoteOrUncPath_ReturnsTrue_ForRemoteOrUncPaths(string path)
    {
        Assert.IsTrue(ClipboardHelper.IsRemoteOrUncPath(path), $"Expected '{path}' to be treated as remote/UNC.");
    }

    [DataTestMethod]
    [DataRow(@"C:\Users\test\file.txt")]
    [DataRow(@"C:\temp")]
    [DataRow(@"C:/Users/test/file.txt")]
    [DataRow(@"\\?\C:\Users\test\file.txt")]
    [DataRow(@"\\?\C:/Users/test/file.txt")]
    [DataRow(@"relative\file.txt")]
    [DataRow(@"file.txt")]
    public void IsRemoteOrUncPath_ReturnsFalse_ForLocalPaths(string path)
    {
        Assert.IsFalse(ClipboardHelper.IsRemoteOrUncPath(path), $"Expected '{path}' to be treated as local.");
    }

    [TestMethod]
    public void IsRemoteOrUncPath_ReturnsTrue_ForUnavailableDriveRoot()
    {
        string[] existingDriveRoots = DriveInfo.GetDrives().Select(drive => drive.Name.ToUpperInvariant()).ToArray();
        char unusedDriveLetter = Enumerable.Range('D', 'Z' - 'D' + 1)
            .Select(value => (char)value)
            .FirstOrDefault(letter => !existingDriveRoots.Contains($@"{letter}:\"));

        if (unusedDriveLetter == default)
        {
            Assert.Inconclusive("No unused drive letter is available to validate unavailable drive root rejection.");
        }

        string unavailableDrivePath = $@"{unusedDriveLetter}:\file.txt";
        Assert.IsTrue(ClipboardHelper.IsRemoteOrUncPath(unavailableDrivePath), $"Expected '{unavailableDrivePath}' to be rejected.");

        string unavailableExtendedLengthDrivePath = $@"\\?\{unusedDriveLetter}:\file.txt";
        Assert.IsTrue(ClipboardHelper.IsRemoteOrUncPath(unavailableExtendedLengthDrivePath), $"Expected '{unavailableExtendedLengthDrivePath}' to be rejected.");
    }

    [DataTestMethod]
    [DataRow(@"//?/UNC/server/share/file.txt")]
    [DataRow(@"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\file.txt")]
    [DataRow("C:\\invalid\0path")]
    public void IsRemoteOrUncPath_ReturnsTrue_ForForwardSlashDeviceOrMalformedPaths(string path)
    {
        Assert.IsTrue(ClipboardHelper.IsRemoteOrUncPath(path), $"Expected '{path}' to be rejected.");
    }

    [TestMethod]
    public void IsRemoteOrUncPath_ReturnsTrue_ForLocalPathThroughRemoteSymbolicLink()
    {
        string testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string linkPath = Path.Combine(testDirectory, "remote-link");

        Directory.CreateDirectory(testDirectory);
        try
        {
            _ = Directory.CreateSymbolicLink(linkPath, @"\\localhost\ClipboardHelperTest");

            Assert.IsTrue(
                ClipboardHelper.IsRemoteOrUncPath(Path.Combine(linkPath, "file.txt")),
                "A local path that traverses a symbolic link must be rejected.");
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void IsRemoteOrUncPath_ReturnsFalse_ForNullOrEmpty(string path)
    {
        // Null/empty are not remote; the downstream File.Exists/Directory.Exists
        // checks handle them safely (treated as "not found").
        Assert.IsFalse(ClipboardHelper.IsRemoteOrUncPath(path));
    }
}
