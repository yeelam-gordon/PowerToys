// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.SettingsBackupRestore.Security;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SettingsBackupAndRestoreUtils
    {
        private static readonly Lock BackupSettingsInternalLock = new();
        private static SettingsBackupAndRestoreUtils instance;
        private (bool Success, string Severity, bool LastBackupExists, DateTime? LastRan) lastBackupSettingsResults;

        private SettingsBackupAndRestoreUtils()
        {
            LastBackupStartTime = DateTime.MinValue;
        }

        public static SettingsBackupAndRestoreUtils Instance => instance ??= new SettingsBackupAndRestoreUtils();

        public DateTime LastBackupStartTime { get; set; }

        public static void SetRegSettingsBackupAndRestoreItem(string itemName, string itemValue)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft", true))
            using (var ptKey = key.OpenSubKey("PowerToys", true) ?? key.CreateSubKey("PowerToys"))
            {
                ptKey.SetValue(itemName, itemValue);
            }
        }

        public static string GetRegSettingsBackupAndRestoreRegItem(string itemName)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\PowerToys"))
            {
                return key?.GetValue(itemName)?.ToString();
            }
        }

        public (bool Success, string Message, string Severity) RestoreSettings(string appBasePath, string settingsBackupAndRestoreDir)
        {
            try
            {
                if (!Directory.Exists(appBasePath))
                {
                    Logger.LogError($"Invalid settings directory {appBasePath}");
                    return (false, "General_SettingsBackupAndRestore_RestoreError", "Error");
                }

                if (string.IsNullOrEmpty(settingsBackupAndRestoreDir))
                {
                    return (false, "General_SettingsBackupAndRestore_NoBackupSyncPath", "Error");
                }

                if (!Directory.Exists(settingsBackupAndRestoreDir))
                {
                    Logger.LogError($"Invalid settingsBackupAndRestoreDir {settingsBackupAndRestoreDir}");
                    return (false, "General_SettingsBackupAndRestore_InvalidBackupLocation", "Error");
                }

                if (SettingsBackupRestoreEngine.GetLatestArchiveFileName(settingsBackupAndRestoreDir) == null)
                {
                    return (false, "General_SettingsBackupAndRestore_NoBackupsFound", "Warning");
                }

                var engine = CreateEngine();
                RestoreOperationResult result = engine.Restore(appBasePath, settingsBackupAndRestoreDir, Path.GetTempPath());
                LogStagingCleanupError(result.StagingCleanupError);
                if (result.SettingsChanged)
                {
                    return (result.RestartRequired, "RESTART APP", "Success");
                }

                return (false, "General_SettingsBackupAndRestore_NothingToRestore", "Informational");
            }
            catch (JsonException ex)
            {
                LogBackupOrRestoreError("Invalid settings JSON during restore.", ex);
                return (false, "General_SettingsBackupAndRestore_SettingsFormatError", "Error");
            }
            catch (Exception ex)
            {
                LogBackupOrRestoreError("Error restoring settings.", ex);
                return (false, "General_SettingsBackupAndRestore_RestoreError", "Error");
            }
        }

        public string GetSettingsBackupAndRestoreDir()
        {
            string settingsBackupAndRestoreDir = GetRegSettingsBackupAndRestoreRegItem("SettingsBackupAndRestoreDir");
            if (settingsBackupAndRestoreDir == null)
            {
                settingsBackupAndRestoreDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PowerToys\\Backup");
            }

            return settingsBackupAndRestoreDir;
        }

        public string GetLatestBackupFileName()
        {
            string settingsBackupAndRestoreDir = GetSettingsBackupAndRestoreDir();
            return string.IsNullOrEmpty(settingsBackupAndRestoreDir)
                ? string.Empty
                : SettingsBackupRestoreEngine.GetLatestArchiveFileName(settingsBackupAndRestoreDir) ?? string.Empty;
        }

        public JsonNode GetLatestSettingsBackupManifest()
        {
            return CreateEngine().GetLatestManifest(GetSettingsBackupAndRestoreDir());
        }

        public (bool Success, string Message, string Severity, bool LastBackupExists, string OptionalMessage) BackupSettings(string appBasePath, string settingsBackupAndRestoreDir, bool dryRun)
        {
            var sw = Stopwatch.StartNew();
            var results = BackupSettingsInternal(appBasePath, settingsBackupAndRestoreDir, dryRun);
            sw.Stop();
            Logger.LogInfo($"BackupSettings took {sw.ElapsedMilliseconds}");
            lastBackupSettingsResults = (results.Success, results.Severity, results.LastBackupExists, DateTime.UtcNow);
            return results;
        }

        public (bool Success, string Message, string Severity, bool LastBackupExists, string OptionalMessage) DryRunBackup()
        {
            var settingsUtils = SettingsUtils.Default;
            var appBasePath = Path.GetDirectoryName(settingsUtils.GetSettingsFilePath());
            return BackupSettings(appBasePath, GetSettingsBackupAndRestoreDir(), true);
        }

        public (bool Success, bool HadError, bool LastBackupExists, DateTime? LastRan) GetLastBackupSettingsResults()
        {
            return (lastBackupSettingsResults.Success, lastBackupSettingsResults.Severity == "Error", lastBackupSettingsResults.LastBackupExists, lastBackupSettingsResults.LastRan);
        }

        private (bool Success, string Message, string Severity, bool LastBackupExists, string OptionalMessage) BackupSettingsInternal(string appBasePath, string settingsBackupAndRestoreDir, bool dryRun)
        {
            var lastBackupExists = false;
            lock (BackupSettingsInternalLock)
            {
                try
                {
                    if (!Directory.Exists(appBasePath))
                    {
                        Logger.LogError($"Invalid settings directory {appBasePath}");
                        return (false, "General_SettingsBackupAndRestore_NoSettingsFilesFound", "Error", lastBackupExists, string.Empty);
                    }

                    if (string.IsNullOrEmpty(settingsBackupAndRestoreDir))
                    {
                        return (false, "General_SettingsBackupAndRestore_NoBackupSyncPath", "Error", lastBackupExists, string.Empty);
                    }

                    if (!Path.IsPathRooted(settingsBackupAndRestoreDir) ||
                        SecurePath.IsContained(Path.GetFullPath(appBasePath), Path.GetFullPath(settingsBackupAndRestoreDir)))
                    {
                        Logger.LogError("Backup location must be rooted and outside the settings directory.");
                        return (false, "General_SettingsBackupAndRestore_InvalidBackupLocation", "Error", lastBackupExists, string.Empty);
                    }

                    BackupOperationResult result = CreateEngine().Backup(
                        appBasePath,
                        settingsBackupAndRestoreDir,
                        Path.GetTempPath(),
                        dryRun,
                        Helper.GetProductVersion(),
                        Environment.MachineName,
                        DateTime.UtcNow);
                    lastBackupExists = result.PreviousBackupExists;
                    LogStagingCleanupError(result.StagingCleanupError);
                    if (result.RetentionError != null)
                    {
                        Logger.LogError("The settings backup was created, but older backups could not all be removed.", result.RetentionError);
                    }

                    if (!result.BackupCreated)
                    {
                        return (false, "General_SettingsBackupAndRestore_NothingToBackup", "Informational", lastBackupExists, string.Empty);
                    }

                    return (true, "General_SettingsBackupAndRestore_BackupComplete", "Success", lastBackupExists, string.Empty);
                }
                catch (JsonException ex)
                {
                    LogBackupOrRestoreError("Invalid settings JSON during backup.", ex);
                    return (false, "General_SettingsBackupAndRestore_SettingsFormatError", "Error", lastBackupExists, string.Empty);
                }
                catch (Exception ex)
                {
                    LogBackupOrRestoreError("Error backing up settings.", ex);
                    return (false, "General_SettingsBackupAndRestore_BackupError", "Error", lastBackupExists, string.Empty);
                }
            }
        }

        private static void LogBackupOrRestoreError(string message, Exception exception)
        {
            Logger.LogError(message, exception);
            if (exception.Data["StagingCleanupError"] is Exception cleanupError)
            {
                LogStagingCleanupError(cleanupError);
            }

            if (exception.Data["RestoreRollbackErrors"] is Exception[] rollbackErrors)
            {
                foreach (Exception rollbackError in rollbackErrors)
                {
                    Logger.LogError("A settings change could not be undone after restore failed.", rollbackError);
                }
            }
        }

        private static void LogStagingCleanupError(Exception cleanupError)
        {
            if (cleanupError != null)
            {
                Logger.LogError("Temporary settings files could not be removed.", cleanupError);
            }
        }

        private static SettingsBackupRestoreEngine CreateEngine()
        {
            return new SettingsBackupRestoreEngine(GetBackupRestoreSettingsJson());
        }

        private static string GetBackupRestoreSettingsJson()
        {
            using Stream stream = typeof(SettingsBackupAndRestoreUtils).Assembly.GetManifestResourceStream("PowerToys.Settings.BackupRestoreSettings.json")
                ?? throw new FileNotFoundException("The embedded backup_restore_settings.json resource could not be found.");
            using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
    }
}
