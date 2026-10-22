// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
public class SettingsBackupAndRestoreTests
{
    [TestMethod]
    [DataRow(false, "General_SettingsBackupAndRestore_RestoreError", "")]
    [DataRow(true, "General_SettingsBackupAndRestore_RestoreSettingsFormatError", "{")]
    [DataRow(true, "General_SettingsBackupAndRestore_RestoreSettingsFormatError", "")]
    [DataRow(true, "General_SettingsBackupAndRestore_RestoreSettingsFormatError", " \t\r\n ")]
    public void RestoreReportsArchiveAndJsonErrorsWithoutChangingSettings(bool validArchive, string expectedMessage, string archivedSettings)
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
                writer.Write(archivedSettings);
            }
            else
            {
                File.WriteAllText(archivePath, "not an archive");
            }

            var result = SettingsBackupAndRestoreUtils.Instance.RestoreSettings(settings, backups);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Error", result.Severity);
            Assert.AreEqual(expectedMessage, result.Message);
            string message = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "BackupRestore.Resources.resw"))
                .Root.Elements("data").Single(item => item.Attribute("name").Value == result.Message).Element("value").Value;
            Assert.IsFalse(message.TrimEnd().EndsWith(':'));
            Assert.AreEqual("""{"value":"current"}""", File.ReadAllText(current));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \t\r\n ")]
    [DoNotParallelize]
    public void BackupReportsEmptyJsonAsMalformedSettings(string contents)
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys-BackupMessages-");
        using StringWriter output = new();
        using TextWriterTraceListener listener = new(output);
        Trace.Listeners.Add(listener);
        try
        {
            string settings = Directory.CreateDirectory(Path.Combine(directory.FullName, "settings")).FullName;
            string backups = Directory.CreateDirectory(Path.Combine(directory.FullName, "backups")).FullName;
            File.WriteAllText(Path.Combine(settings, "settings.json"), contents);

            var result = SettingsBackupAndRestoreUtils.Instance.BackupSettings(settings, backups, true);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Error", result.Severity);
            Assert.AreEqual("General_SettingsBackupAndRestore_SettingsFormatError", result.Message);
            Assert.AreEqual("settings.json", result.OptionalMessage.Trim());
            StringAssert.Contains(output.ToString(), "Settings file: settings.json");
            Assert.AreEqual(0, Directory.GetFiles(backups).Length);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BackupReportsNoIncludedSettingsFiles(bool unrelatedFile)
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys-BackupMessages-");
        try
        {
            string settings = Directory.CreateDirectory(Path.Combine(directory.FullName, "settings")).FullName;
            string backups = Directory.CreateDirectory(Path.Combine(directory.FullName, "backups")).FullName;
            if (unrelatedFile)
            {
                File.WriteAllText(Path.Combine(settings, "notes.txt"), "not settings");
            }

            var result = SettingsBackupAndRestoreUtils.Instance.BackupSettings(settings, backups, true);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Error", result.Severity);
            Assert.AreEqual("General_SettingsBackupAndRestore_NoSettingsFilesFound", result.Message);
            Assert.AreEqual(0, Directory.GetFiles(backups).Length);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void OperationErrorLogsEveryAttachedCleanupFailure()
    {
        Exception primary = new IOException("primary operation failure");
        string[] singularKeys = ["StagingCleanupError", "ArchiveCleanupError", "CreatedFileCleanupError", "CreatedDirectoryCleanupError"];
        string[] arrayKeys = ["StagingCleanupErrors", "RestoreRollbackErrors"];
        foreach (string key in singularKeys)
        {
            primary.Data[key] = new IOException(key);
        }

        foreach (string key in arrayKeys)
        {
            primary.Data[key] = new Exception[] { new IOException(key + "-first"), new IOException(key + "-second") };
        }

        using StringWriter output = new();
        using TextWriterTraceListener listener = new(output);
        Trace.Listeners.Add(listener);
        try
        {
            MethodInfo method = typeof(SettingsBackupAndRestoreUtils).GetMethod("LogBackupOrRestoreError", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { "test operation failure", primary });
            listener.Flush();
            string log = output.ToString();
            StringAssert.Contains(log, primary.Message);
            foreach (string key in singularKeys)
            {
                StringAssert.Contains(log, key);
            }

            foreach (string key in arrayKeys)
            {
                StringAssert.Contains(log, key + "-first");
                StringAssert.Contains(log, key + "-second");
            }
        }
        finally
        {
            Trace.Listeners.Remove(listener);
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
