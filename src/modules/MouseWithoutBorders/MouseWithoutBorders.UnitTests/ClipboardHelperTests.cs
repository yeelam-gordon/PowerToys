// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

// Guards the fix for MSRC 110760 / ICM 31000000569630: the ClipboardHelper IPC
// endpoint must reject UNC/remote paths so a malicious pipe client cannot coerce
// outbound SMB authentication (NTLMv2 hash leak) by injecting a path that is then
// probed via File.Exists/Directory.Exists.
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
