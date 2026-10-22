// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security.UnitTests;

[TestClass]
public sealed class SettingsBackupRestoreEngineTests
{
    private static readonly string[] ExpectedArchiveEntries = ["settings.json", "FancyZones/settings.json", "manifest.json"];

    private const string PolicyJson = """
        {
          "IncludeFiles": ["*.json"],
          "IgnoreFiles": ["*\\ignored.json"],
          "IgnoredSettings": {
            "settings.json": ["machineOnly"]
          },
          "CustomRestoreSettings": {
            "\\Keyboard Manager\\default.json": { "overwrite": true }
          },
          "RestartAfterRestore": true
        }
        """;

    [TestMethod]
    public void ProductionBackupWritesLegacyArchiveAndDryRunDoesNotWrite()
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"settings\settings.json", """{"machineOnly":"x","value":1}""");
        test.CreateFile(@"settings\FancyZones\settings.json", """{"zone":1}""");
        test.CreateFile(@"settings\PowerToys\ignored.json", """{"ignored":true}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = new(2026, 8, 16, 7, 0, 0, DateTimeKind.Utc);

        BackupOperationResult first = engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", now);
        BackupOperationResult unchanged = engine.Backup(settings, backups, staging, true, "1.2.3", "test-machine", now.AddMinutes(1));

        Assert.IsTrue(first.BackupCreated);
        Assert.IsFalse(first.PreviousBackupExists);
        Assert.IsFalse(unchanged.BackupCreated);
        Assert.IsTrue(unchanged.PreviousBackupExists);
        Assert.AreEqual(1, Directory.GetFiles(backups, "*.ptb").Length);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);

        using ZipArchive archive = ZipFile.OpenRead(Path.Combine(backups, first.ArchiveFileName!));
        CollectionAssert.AreEquivalent(
            ExpectedArchiveEntries,
            archive.Entries.Select(entry => entry.FullName).ToArray());
        using StreamReader settingsReader = new(archive.GetEntry("settings.json")!.Open());
        using JsonDocument exported = JsonDocument.Parse(settingsReader.ReadToEnd());
        Assert.IsFalse(exported.RootElement.TryGetProperty("machineOnly", out _));
    }

    [TestMethod]
    public void BackupPreservesExistingArchiveWhenTimestampCollides()
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string settingsFile = test.CreateFile(@"settings\settings.json", """{"value":1}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = new(2026, 8, 16, 7, 0, 0, DateTimeKind.Utc);
        BackupOperationResult first = engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", now);
        string firstArchivePath = Path.Combine(backups, first.ArchiveFileName!);
        byte[] firstArchive = File.ReadAllBytes(firstArchivePath);
        File.WriteAllText(settingsFile, """{"value":2}""");

        BackupOperationResult second = engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", now);

        Assert.IsTrue(second.BackupCreated);
        Assert.AreNotEqual(first.ArchiveFileName, second.ArchiveFileName);
        CollectionAssert.AreEqual(firstArchive, File.ReadAllBytes(firstArchivePath));
        Assert.AreEqual(2, Directory.GetFiles(backups, "*.ptb").Length);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void RetentionFailureDoesNotUndoASuccessfulBackup()
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string settingsFile = test.CreateFile(@"settings\settings.json", """{"value":1}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = DateTime.UtcNow;
        DateTime oldTime = now.AddDays(-90);
        BackupOperationResult first = engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", oldTime);
        string oldArchive = Path.Combine(backups, first.ArchiveFileName!);
        for (int index = 1; index <= 10; index++)
        {
            File.Copy(oldArchive, Path.Combine(backups, $"settings_{oldTime.ToFileTimeUtc() + index}.ptb"));
        }

        File.SetAttributes(oldArchive, FileAttributes.ReadOnly);
        try
        {
            File.WriteAllText(settingsFile, """{"value":2}""");

            BackupOperationResult result = engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", now);

            Assert.IsTrue(result.BackupCreated);
            Assert.IsNotNull(result.RetentionError);
            Assert.IsTrue(File.Exists(Path.Combine(backups, result.ArchiveFileName!)));
            Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
        }
        finally
        {
            if (File.Exists(oldArchive))
            {
                File.SetAttributes(oldArchive, FileAttributes.Normal);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StagingCleanupDoesNotReplaceCompletedOperation(bool restore)
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string sourceSettings = test.CreateFile(@"source\settings.json", """{"value":1}""");
        test.CreateFile(@"target\settings.json", """{"value":0}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = DateTime.UtcNow;
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", now);
        File.WriteAllText(sourceSettings, """{"value":2}""");
        FileStream? stagingLock = null;
        engine.BeforeStagingCleanup = root =>
            stagingLock = new FileStream(Path.Combine(root.FinalPath, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (restore)
            {
                RestoreOperationResult result = engine.Restore(target, backups, staging);
                Assert.IsNotNull(result.StagingCleanupError);
                Assert.IsTrue(result.SettingsChanged);
                Assert.IsTrue(result.RestartRequired);
                using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, "settings.json")));
                Assert.AreEqual(1, settings.RootElement.GetProperty("value").GetInt32());
            }
            else
            {
                BackupOperationResult result = engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", now.AddMinutes(1));
                Assert.IsNotNull(result.StagingCleanupError);
                Assert.IsTrue(result.BackupCreated);
                Assert.IsTrue(File.Exists(Path.Combine(backups, result.ArchiveFileName!)));
            }
        }
        finally
        {
            stagingLock?.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StagingCleanupDoesNotReplaceOriginalFailure(bool restore)
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\settings.json", "{}");
        using (ZipArchive archive = ZipFile.Open(Path.Combine(backups, "settings_1.ptb"), ZipArchiveMode.Create))
        {
            using StreamWriter writer = new(archive.CreateEntry("settings.json").Open());
            writer.Write("{");
        }

        SettingsBackupRestoreEngine engine = new(PolicyJson);
        FileStream? stagingLock = null;
        engine.BeforeStagingCleanup = root =>
            stagingLock = new FileStream(Path.Combine(root.FinalPath, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            try
            {
                if (restore)
                {
                    engine.Restore(source, backups, staging);
                }
                else
                {
                    engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
                }

                Assert.Fail("Invalid settings must retain their JSON error.");
            }
            catch (JsonException exception)
            {
                Assert.IsInstanceOfType<System.ComponentModel.Win32Exception>(exception.Data["StagingCleanupError"]);
            }
        }
        finally
        {
            stagingLock?.Dispose();
        }
    }

    [TestMethod]
    public void ProductionBackupManifestAndChangedDryRunPreserveCompatibility()
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"settings\settings.json", """{"value":1}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = new(2026, 8, 16, 7, 0, 0, DateTimeKind.Utc);
        engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", now);
        File.WriteAllText(Path.Combine(settings, "settings.json"), """{"value":2}""");

        BackupOperationResult dryRun = engine.Backup(settings, backups, staging, true, "1.2.3", "test-machine", now.AddMinutes(1));
        JsonNode? manifest = engine.GetLatestManifest(backups);

        Assert.IsTrue(dryRun.BackupCreated);
        Assert.AreEqual(1, Directory.GetFiles(backups, "*.ptb").Length);
        Assert.AreEqual("1.2.3", manifest!["Version"]!.GetValue<string>());
        Assert.AreEqual("test-machine", manifest["BackupSource"]!.GetValue<string>());
        Assert.AreEqual(@"\settings.json", manifest["UpdatedFiles"]![0]!.GetValue<string>());
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    [DataRow("manifest.json")]
    [DataRow("MANIFEST.JSON")]
    public void LatestManifestPreservesWindowsNamesAndTextEncoding(string manifestName)
    {
        using TestDirectory test = new();
        string backups = test.CreateDirectory("backups");
        using (ZipArchive archive = ZipFile.Open(Path.Combine(backups, "settings_1.ptb"), ZipArchiveMode.Create))
        {
            using StreamWriter writer = new(archive.CreateEntry(manifestName).Open(), Encoding.Unicode);
            writer.Write("""{"BackupSource":"test-machine"}""");
        }

        SettingsBackupRestoreEngine engine = new(PolicyJson);

        JsonNode? manifest = engine.GetLatestManifest(backups);

        Assert.AreEqual("test-machine", manifest!["BackupSource"]!.GetValue<string>());
        Assert.AreEqual(1, Directory.GetFileSystemEntries(backups).Length);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(100)]
    public void ManifestInspectionRejectsEntryLengthMismatch(int declaredLength)
    {
        using TestDirectory test = new();
        string backups = test.CreateDirectory("backups");
        test.CreateStoredArchiveWithDeclaredLength(@"backups\settings_1.ptb", "manifest.json", """{"BackupSource":"test-machine"}""", declaredLength);
        SettingsBackupRestoreEngine engine = new(PolicyJson);

        Assert.ThrowsException<InvalidDataException>(() => engine.GetLatestManifest(backups));
    }

    [TestMethod]
    public void ProductionRestoreUsesCreateMergeOverwriteIgnoreAndRestart()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\settings.json", """{"backup":true,"shared":"backup"}""");
        test.CreateFile(@"source\FancyZones\settings.json", """{"fromBackup":true,"shared":"backup"}""");
        test.CreateFile(@"source\Keyboard Manager\default.json", """{"overwrite":"backup"}""");
        test.CreateFile(@"source\NewModule\settings.json", """{"created":true}""");
        test.CreateFile(@"source\PowerToys\ignored.json", """{"ignored":"backup"}""");
        test.CreateFile(@"target\settings.json", """{"current":true,"shared":"current"}""");
        test.CreateFile(@"target\FancyZones\settings.json", """{"currentOnly":true,"shared":"current"}""");
        test.CreateFile(@"target\Keyboard Manager\default.json", """{"overwrite":"current","keep":true}""");
        test.CreateFile(@"target\PowerToys\ignored.json", """{"ignored":"current"}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);

        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsTrue(result.SettingsChanged);
        Assert.IsTrue(result.RestartRequired);
        using JsonDocument merged = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, @"FancyZones\settings.json")));
        Assert.IsTrue(merged.RootElement.GetProperty("currentOnly").GetBoolean());
        Assert.IsTrue(merged.RootElement.GetProperty("fromBackup").GetBoolean());
        Assert.AreEqual("backup", merged.RootElement.GetProperty("shared").GetString());
        using JsonDocument overwritten = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, @"Keyboard Manager\default.json")));
        Assert.IsFalse(overwritten.RootElement.TryGetProperty("keep", out _));
        Assert.AreEqual("backup", overwritten.RootElement.GetProperty("overwrite").GetString());
        Assert.IsTrue(File.Exists(Path.Combine(target, @"NewModule\settings.json")));
        Assert.AreEqual("""{"ignored":"current"}""", File.ReadAllText(Path.Combine(target, @"PowerToys\ignored.json")));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void RestoreSkipsAnUnchangedEffectiveMerge()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\settings.json", """{"items":[1,2]}""");
        string current = test.CreateFile(@"target\settings.json", """{"items":[2,1],"currentOnly":true}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);

        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsFalse(result.SettingsChanged);
        Assert.IsFalse(result.RestartRequired);
        Assert.AreEqual("""{"items":[2,1],"currentOnly":true}""", File.ReadAllText(current));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RestoreSkipsUnchangedReadOnlyFilesAndRestoresWritableFiles(bool effectiveMerge)
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\same.json", """{"value":1}""");
        test.CreateFile(@"source\writable.json", """{"value":2}""");
        string original = effectiveMerge ? """{"value":1,"currentOnly":true}""" : """{"value":1}""";
        string readOnly = test.CreateFile(@"target\same.json", original);
        string writable = test.CreateFile(@"target\writable.json", """{"value":0}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        try
        {
            RestoreOperationResult result = engine.Restore(target, backups, staging);

            Assert.IsTrue(result.SettingsChanged);
            Assert.IsTrue(result.RestartRequired);
            Assert.AreEqual(original, File.ReadAllText(readOnly));
            Assert.IsTrue((File.GetAttributes(readOnly) & FileAttributes.ReadOnly) != 0);
            using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(writable));
            Assert.AreEqual(2, settings.RootElement.GetProperty("value").GetInt32());
            Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
        }
        finally
        {
            File.SetAttributes(readOnly, FileAttributes.Normal);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RestoreRecomputesChangesAfterAcquiringTheWritableHandle(bool becomesUnchanged)
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\settings.json", """{"value":1}""");
        string current = test.CreateFile(@"target\settings.json", """{"value":2,"currentOnly":"first"}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        bool changedBeforeOpen = false;
        engine.BeforeRestoreFileOverwrite = relativePath =>
        {
            Assert.AreEqual("settings.json", relativePath);
            File.WriteAllText(current, becomesUnchanged ? """{"value":1,"currentOnly":"second"}""" : """{"value":3,"currentOnly":"second"}""");
            changedBeforeOpen = true;
        };

        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsTrue(changedBeforeOpen);
        Assert.AreEqual(!becomesUnchanged, result.SettingsChanged);
        Assert.AreEqual(!becomesUnchanged, result.RestartRequired);
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(current));
        Assert.AreEqual(1, settings.RootElement.GetProperty("value").GetInt32());
        Assert.AreEqual("second", settings.RootElement.GetProperty("currentOnly").GetString());
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RestoreDoesNotOpenCurrentFilesAbsentFromTheArchive(bool redirectedDirectory)
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\Keyboard Manager\default.json", """{"value":1}""");
        string current = test.CreateFile(@"target\Keyboard Manager\default.json", """{"value":0}""");
        string unrelated;
        if (redirectedDirectory)
        {
            string outside = test.CreateDirectory("outside");
            unrelated = test.CreateFile(@"outside\settings.json", """{"untouched":true}""");
            test.CreateDirectoryJunction(@"target\FancyZones", outside);
        }
        else
        {
            unrelated = test.CreateFile(@"target\FancyZones\settings.json", """{"untouched":true}""");
        }

        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        using (FileStream unrelatedLock = new(unrelated, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.IsTrue(engine.Restore(target, backups, staging).SettingsChanged);
        }

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(current));
        Assert.AreEqual(1, settings.RootElement.GetProperty("value").GetInt32());
        Assert.AreEqual("""{"untouched":true}""", File.ReadAllText(unrelated));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void RestoreSkipsReorderedPowerToysRunPlugins()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(
            @"source\PowerToys Run\settings.json",
            """{"plugins":[{"Id":"other","Name":"Other"},{"Id":"525995402BEF4A8CA860D92F6D108092","Name":"Plugin"}]}""");
        const string currentJson = """
            {"plugins":[{"Name":"Plugin","Id":"525995402BEF4A8CA860D92F6D108092","IconPathDark":"dark","IconPathLight":"light"},{"Name":"Other","Id":"other"}]}
            """;
        string current = test.CreateFile(@"target\PowerToys Run\settings.json", currentJson);
        SettingsBackupRestoreEngine engine = new(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "backup_restore_settings.json")));
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);

        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsFalse(result.SettingsChanged);
        Assert.IsFalse(result.RestartRequired);
        Assert.AreEqual(currentJson, File.ReadAllText(current));
    }

    [TestMethod]
    public void RestorePreservesArrayOrderForOverwrite()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\Keyboard Manager\default.json", """{"items":[1,2]}""");
        string current = test.CreateFile(@"target\Keyboard Manager\default.json", """{"items":[2,1]}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);

        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsTrue(result.SettingsChanged);
        Assert.IsTrue(result.RestartRequired);
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(current));
        Assert.AreEqual(1, settings.RootElement.GetProperty("items")[0].GetInt32());
        Assert.AreEqual(2, settings.RootElement.GetProperty("items")[1].GetInt32());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RestoreRollbackRemovesCreatedFileAndPreservesReplacement(bool replaceAfterClose)
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\existing.json", """{"value":2}""");
        test.CreateFile(@"source\created.json", """{"created":true}""");
        const string originalJson = """{"value":1}""";
        string existing = test.CreateFile(@"target\existing.json", originalJson);
        string created = Path.Combine(target, "created.json");
        const string replacementJson = """{"replacement":true}""";
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        IOException failure = new("Injected restore write failure.");
        int writeCount = 0;
        engine.AfterRestoreWrite = _ =>
        {
            if (++writeCount == 2)
            {
                throw failure;
            }
        };
        engine.AfterRestoreRollbackFileClosed = relativePath =>
        {
            Assert.AreEqual("created.json", relativePath);
            if (replaceAfterClose)
            {
                File.Delete(created);
                File.WriteAllText(created, replacementJson);
            }
        };

        IOException actual = Assert.ThrowsException<IOException>(() => engine.Restore(target, backups, staging));

        Assert.AreSame(failure, actual);
        Assert.AreEqual(2, writeCount);
        Assert.AreEqual(originalJson, File.ReadAllText(existing));
        if (replaceAfterClose)
        {
            Assert.IsTrue(File.Exists(created));
            Assert.AreEqual(replacementJson, File.ReadAllText(created));
        }
        else
        {
            Assert.IsFalse(File.Exists(created));
        }

        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BackupUsesTheRestoreModeArrayComparison(bool overwrite)
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string relativePath = overwrite ? @"Keyboard Manager\default.json" : "settings.json";
        string current = test.CreateFile("settings\\" + relativePath, """{"items":[1,2]}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = DateTime.UtcNow;
        engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", now);
        File.WriteAllText(current, """{"items":[2,1]}""");

        BackupOperationResult dryRun = engine.Backup(settings, backups, staging, true, "1.2.3", "test-machine", now.AddMinutes(1));
        BackupOperationResult backup = engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", now.AddMinutes(1));

        Assert.AreEqual(overwrite, dryRun.BackupCreated);
        Assert.AreEqual(overwrite, backup.BackupCreated);
        Assert.AreEqual(overwrite ? 2 : 1, Directory.GetFiles(backups, "*.ptb").Length);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RestoreRollbackPreservesConcurrentDirectories(bool replaceAfterClose)
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\Module\settings.json", """{"value":1}""");
        string module = Path.Combine(target, "Module");
        string moved = Path.Combine(target, "moved");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        if (!replaceAfterClose)
        {
            engine.BeforeRestoreFileCreate = _ => Directory.CreateDirectory(module);
        }

        IOException failure = new("Injected restore write failure.");
        engine.AfterRestoreWrite = _ => throw failure;
        engine.AfterRestoreRollbackFileClosed = _ =>
        {
            if (replaceAfterClose)
            {
                Directory.Move(module, moved);
                Directory.CreateDirectory(module);
            }
        };

        IOException actual = Assert.ThrowsException<IOException>(() => engine.Restore(target, backups, staging));

        Assert.AreSame(failure, actual);
        Assert.IsTrue(Directory.Exists(module));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(module).Length);
        Assert.IsFalse(File.Exists(Path.Combine(module, "settings.json")));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void BackupDoesNotPublishAnArchiveBeyondRestoreLimits()
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string relativePath = string.Join('\\', Enumerable.Repeat("d", BackupRestoreArchive.MaximumPathDepth)) + "\\settings.json";
        test.CreateFile("settings\\" + relativePath, "{}");
        SettingsBackupRestoreEngine engine = new(PolicyJson);

        Assert.ThrowsException<InvalidDataException>(() =>
            engine.Backup(settings, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow));

        Assert.AreEqual(0, Directory.GetFileSystemEntries(backups).Length);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void ProductionPolicyIgnoresRootRuntimeSettingsInBackupAndRestore()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\settings.json", """{"value":1}""");
        test.CreateFile(@"source\log_settings.json", """{"value":"backup"}""");
        test.CreateFile(@"source\oobe_settings.json", """{"value":"backup"}""");
        string log = test.CreateFile(@"target\log_settings.json", """{"value":"current"}""");
        string oobe = test.CreateFile(@"target\oobe_settings.json", """{"value":"current"}""");
        SettingsBackupRestoreEngine engine = new(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "backup_restore_settings.json")));

        BackupOperationResult backup = engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        using (ZipArchive archive = ZipFile.Open(Path.Combine(backups, backup.ArchiveFileName!), ZipArchiveMode.Update))
        {
            Assert.IsNull(archive.GetEntry("log_settings.json"));
            Assert.IsNull(archive.GetEntry("oobe_settings.json"));
            using (StreamWriter writer = new(archive.CreateEntry("log_settings.json").Open()))
            {
                writer.Write("""{"value":"old-backup"}""");
            }

            using StreamWriter oobeWriter = new(archive.CreateEntry("oobe_settings.json").Open());
            oobeWriter.Write("""{"value":"old-backup"}""");
        }

        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsTrue(result.SettingsChanged);
        Assert.AreEqual("""{"value":"current"}""", File.ReadAllText(log));
        Assert.AreEqual("""{"value":"current"}""", File.ReadAllText(oobe));
    }

    [TestMethod]
    public void ProductionEnginePreservesWindowsDistinctUnicodePaths()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\σ.json", """{"name":"sigma"}""");
        test.CreateFile(@"source\ς.json", """{"name":"final-sigma"}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);

        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsTrue(result.SettingsChanged);
        using JsonDocument sigma = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, "σ.json")));
        using JsonDocument finalSigma = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, "ς.json")));
        Assert.AreEqual("sigma", sigma.RootElement.GetProperty("name").GetString());
        Assert.AreEqual("final-sigma", finalSigma.RootElement.GetProperty("name").GetString());
    }

    [TestMethod]
    public void UnrelatedLockedFilesAreNotOpenedDuringFiltering()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string unrelatedTarget = test.CreateDirectory("unrelated-target");
        test.CreateFile(@"source\settings.json", """{"value":1}""");
        string sourceUnrelated = test.CreateFile(@"source\unrelated.bin", "not settings");
        string backupUnrelated = test.CreateFile(@"backups\unrelated.bin", "not an archive");
        test.CreateDirectoryJunction(@"backups\unrelated-junction", unrelatedTarget);
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        using FileStream sourceLock = new(sourceUnrelated, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using FileStream backupLock = new(backupUnrelated, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        BackupOperationResult backup = engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);
        RestoreOperationResult restore = engine.Restore(target, backups, staging);

        Assert.IsTrue(backup.BackupCreated);
        Assert.IsTrue(restore.SettingsChanged);
        Assert.AreEqual("""{"value":1}""", JsonNode.Parse(File.ReadAllText(Path.Combine(target, "settings.json")))!.ToJsonString());
    }

    [TestMethod]
    [DataRow("inspect")]
    [DataRow("backup")]
    [DataRow("dry-run")]
    [DataRow("restore")]
    public void LockedOlderArchiveDoesNotBlockTheLatestArchive(string operation)
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string current = test.CreateFile(@"source\settings.json", """{"value":1}""");
        string targetSettings = test.CreateFile(@"target\settings.json", """{"value":0}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = DateTime.UtcNow;
        BackupOperationResult first = engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", now);
        File.WriteAllText(current, """{"value":2}""");
        BackupOperationResult latest = engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", now.AddMinutes(1));
        File.WriteAllText(current, """{"value":3}""");
        using FileStream olderLock = new(Path.Combine(backups, first.ArchiveFileName!), FileMode.Open, FileAccess.Read, FileShare.None);

        switch (operation)
        {
            case "inspect":
                Assert.AreEqual(latest.ArchiveFileName, SettingsBackupRestoreEngine.GetLatestArchiveFileName(backups));
                Assert.IsNotNull(engine.GetLatestManifest(backups));
                break;
            case "backup":
            case "dry-run":
                BackupOperationResult result = engine.Backup(source, backups, staging, operation == "dry-run", "1.2.3", "test-machine", now.AddMinutes(2));
                Assert.IsTrue(result.BackupCreated);
                Assert.IsNull(result.RetentionError);
                Assert.AreEqual(operation == "dry-run" ? 2 : 3, Directory.GetFiles(backups, "*.ptb").Length);
                break;
            case "restore":
                Assert.IsTrue(engine.Restore(target, backups, staging).SettingsChanged);
                using (JsonDocument settings = JsonDocument.Parse(File.ReadAllText(targetSettings)))
                {
                    Assert.AreEqual(2, settings.RootElement.GetProperty("value").GetInt32());
                }

                break;
            default:
                Assert.Fail("Unknown test operation.");
                break;
        }

        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void SelectedArchiveStillRequiresSingleLinkValidation()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string shared = test.CreateDirectory("shared");
        test.CreateFile(@"source\settings.json", """{"value":1}""");
        string targetSettings = test.CreateFile(@"target\settings.json", """{"value":0}""");
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        DateTime now = DateTime.UtcNow;
        BackupOperationResult first = engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", now);
        string sharedArchive = Path.Combine(shared, "archive.ptb");
        File.Copy(Path.Combine(backups, first.ArchiveFileName!), sharedArchive);
        test.CreateHardLink($"backups\\settings_{now.AddMinutes(1).ToFileTimeUtc()}.ptb", sharedArchive);

        Assert.ThrowsException<IOException>(() => engine.GetLatestManifest(backups));
        Assert.ThrowsException<IOException>(() => engine.Restore(target, backups, staging));
        Assert.AreEqual("""{"value":0}""", File.ReadAllText(targetSettings));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void InvalidArchiveLeavesSettingsUnchanged()
    {
        using TestDirectory test = new();
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string targetSettings = test.CreateFile(@"target\settings.json", """{"value":"current"}""");
        string archivePath = Path.Combine(backups, $"settings_{DateTime.UtcNow.ToFileTimeUtc()}.ptb");
        using (FileStream stream = new(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
        using (StreamWriter writer = new(archive.CreateEntry("../settings.json").Open()))
        {
            writer.Write("""{"value":"untrusted"}""");
        }

        SettingsBackupRestoreEngine engine = new(PolicyJson);

        Assert.ThrowsException<InvalidDataException>(() => engine.Restore(target, backups, staging));
        Assert.AreEqual("""{"value":"current"}""", File.ReadAllText(targetSettings));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BackupRejectsSharedFileEntries(bool dryRun)
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string shared = test.CreateFile(@"shared\settings.json", """{"value":1}""");
        test.CreateHardLink(@"settings\settings.json", shared);
        SettingsBackupRestoreEngine engine = new(PolicyJson);

        Assert.ThrowsException<IOException>(() =>
            engine.Backup(settings, backups, staging, dryRun, "1.2.3", "test-machine", DateTime.UtcNow));

        Assert.AreEqual(0, Directory.GetFileSystemEntries(backups).Length);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
        Assert.AreEqual("""{"value":1}""", File.ReadAllText(shared));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BackupRejectsRedirectedModuleDirectories(bool dryRun)
    {
        using TestDirectory test = new();
        string settings = test.CreateDirectory("settings");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string shared = test.CreateDirectory("shared");
        test.CreateFile(@"shared\settings.json", """{"value":1}""");
        test.CreateDirectoryJunction(@"settings\module", shared);
        SettingsBackupRestoreEngine engine = new(PolicyJson);

        Assert.ThrowsException<IOException>(() =>
            engine.Backup(settings, backups, staging, dryRun, "1.2.3", "test-machine", DateTime.UtcNow));

        Assert.AreEqual(0, Directory.GetFileSystemEntries(backups).Length);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void RestoreValidatesAllExistingTargetsBeforeUpdatingSettings()
    {
        using TestDirectory test = new();
        string source = test.CreateDirectory("source");
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        test.CreateFile(@"source\A\settings.json", """{"value":"new-a"}""");
        test.CreateFile(@"source\Z\settings.json", """{"value":"new-z"}""");
        string first = test.CreateFile(@"target\A\settings.json", """{"value":"old-a"}""");
        string shared = test.CreateFile(@"shared\settings.json", """{"value":"old-z"}""");
        test.CreateHardLink(@"target\Z\settings.json", shared);
        SettingsBackupRestoreEngine engine = new(PolicyJson);
        engine.Backup(source, backups, staging, false, "1.2.3", "test-machine", DateTime.UtcNow);

        Assert.ThrowsException<IOException>(() => engine.Restore(target, backups, staging));

        Assert.AreEqual("""{"value":"old-a"}""", File.ReadAllText(first));
        Assert.AreEqual("""{"value":"old-z"}""", File.ReadAllText(shared));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void RestoreReadsLegacyArchiveAndPreservesNoRestartPolicy()
    {
        using TestDirectory test = new();
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string archivePath = Path.Combine(backups, $"settings_{DateTime.UtcNow.ToFileTimeUtc()}.ptb");
        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            using (StreamWriter writer = new(archive.CreateEntry(@"Module\settings.json").Open()))
            {
                writer.Write("""{"value":1}""");
            }

            using StreamWriter manifest = new(archive.CreateEntry("manifest.json").Open());
            manifest.Write("""{"CreateDateTime":"2026-08-16 07:00:00Z","Version":"1.2.3","UpdatedFiles":["\\Module\\settings.json"],"BackupSource":"test-machine","UnchangedFiles":[]}""");
        }

        SettingsBackupRestoreEngine engine = new(PolicyJson.Replace("\"RestartAfterRestore\": true", "\"RestartAfterRestore\": false", StringComparison.Ordinal));
        RestoreOperationResult result = engine.Restore(target, backups, staging);

        Assert.IsTrue(result.SettingsChanged);
        Assert.IsFalse(result.RestartRequired);
        Assert.AreEqual(1, JsonNode.Parse(File.ReadAllText(Path.Combine(target, @"Module\settings.json")))!["value"]!.GetValue<int>());
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }

    [TestMethod]
    public void MalformedLaterEntryDoesNotPartiallyApplyRestore()
    {
        using TestDirectory test = new();
        string target = test.CreateDirectory("target");
        string backups = test.CreateDirectory("backups");
        string staging = test.CreateDirectory("staging");
        string firstTarget = test.CreateFile(@"target\A\settings.json", """{"value":"current-a"}""");
        string secondTarget = test.CreateFile(@"target\Z\settings.json", """{"value":"current-z"}""");
        string archivePath = Path.Combine(backups, $"settings_{DateTime.UtcNow.ToFileTimeUtc()}.ptb");
        using (FileStream stream = new(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
        {
            using (StreamWriter first = new(archive.CreateEntry("A/settings.json").Open()))
            {
                first.Write("""{"value":"backup-a"}""");
            }

            using StreamWriter second = new(archive.CreateEntry("Z/settings.json").Open());
            second.Write("""{"value":""");
        }

        SettingsBackupRestoreEngine engine = new(PolicyJson);

        try
        {
            engine.Restore(target, backups, staging);
            Assert.Fail("Malformed restore JSON should fail before writes.");
        }
        catch (JsonException)
        {
        }

        Assert.AreEqual("""{"value":"current-a"}""", File.ReadAllText(firstTarget));
        Assert.AreEqual("""{"value":"current-z"}""", File.ReadAllText(secondTarget));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(staging).Length);
    }
}
