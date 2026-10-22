// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
public class SettingsBackupAndRestoreTests
{
    [TestMethod]
    [DataRow(false, "General_SettingsBackupAndRestore_RestoreError")]
    [DataRow(true, "General_SettingsBackupAndRestore_SettingsFormatError")]
    public void RestoreReportsArchiveAndJsonErrorsWithoutChangingSettings(bool validArchive, string expectedMessage)
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys-RestoreMessages-");
        try
        {
            string settings = Directory.CreateDirectory(Path.Combine(directory.FullName, "settings")).FullName;
            string backups = Directory.CreateDirectory(Path.Combine(directory.FullName, "backups")).FullName;
            string current = Path.Combine(settings, "settings.json");
            File.WriteAllText(current, """{"value":"current"}""");
            string archivePath = Path.Combine(backups, $"settings_{DateTime.UtcNow.ToFileTimeUtc()}.ptb");
            if (validArchive)
            {
                using ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
                using StreamWriter writer = new(archive.CreateEntry("settings.json").Open());
                writer.Write("{");
            }
            else
            {
                File.WriteAllText(archivePath, "not an archive");
            }

            var result = SettingsBackupAndRestoreUtils.Instance.RestoreSettings(settings, backups);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Error", result.Severity);
            Assert.AreEqual(expectedMessage, result.Message);
            Assert.AreEqual("""{"value":"current"}""", File.ReadAllText(current));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void RestoreReportsMissingBackupsAsWarning()
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys-RestoreMessages-");
        try
        {
            string settings = Directory.CreateDirectory(Path.Combine(directory.FullName, "settings")).FullName;
            string backups = Directory.CreateDirectory(Path.Combine(directory.FullName, "backups")).FullName;

            var result = SettingsBackupAndRestoreUtils.Instance.RestoreSettings(settings, backups);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Warning", result.Severity);
            Assert.AreEqual("General_SettingsBackupAndRestore_NoBackupsFound", result.Message);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
