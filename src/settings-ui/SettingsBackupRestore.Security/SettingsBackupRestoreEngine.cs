// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Production backup and restore orchestration built on validated handle-relative I/O.
/// </summary>
public sealed class SettingsBackupRestoreEngine
{
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new() { WriteIndented = true };
    private readonly BackupRestorePolicy policy;

    /// <summary>
    /// Initializes a new instance from the existing backup_restore_settings.json contract.
    /// </summary>
    public SettingsBackupRestoreEngine(string policyJson)
    {
        policy = BackupRestorePolicy.Parse(policyJson);
    }

    internal Action<SecureDirectoryRoot>? BeforeStagingCleanup { get; set; }

    internal Action<string>? AfterRestoreWrite { get; set; }

    internal Action<string>? BeforeRestoreFileCreate { get; set; }

    internal Action<string>? AfterRestoreRollbackFileClosed { get; set; }

    /// <summary>
    /// Compares settings with the newest archive and optionally writes a compatible .ptb archive.
    /// </summary>
    public BackupOperationResult Backup(
        string settingsRootPath,
        string backupRootPath,
        string stagingParentPath,
        bool dryRun,
        string productVersion,
        string machineName,
        DateTime utcNow)
    {
        using SecureDirectoryRoot settingsRoot = SecureDirectoryRoot.OpenReadOnly(settingsRootPath);
        IReadOnlyDictionary<string, string> current = ReadSettings(settingsRoot);
        if (current.Count == 0)
        {
            throw new InvalidDataException("No settings files were found.");
        }

        SecureDirectoryRoot? backupRoot = null;
        SecureDirectoryRoot? stagingParent = null;
        SecureDirectoryRoot? previousStaging = null;
        BackupOperationResult? result = null;
        Exception? operationError = null;
        try
        {
            if (Directory.Exists(backupRootPath))
            {
                backupRoot = dryRun ? SecureDirectoryRoot.OpenReadOnly(backupRootPath) : SecureDirectoryRoot.Open(backupRootPath);
            }
            else if (!dryRun)
            {
                backupRoot = SecureDirectoryRoot.OpenOrCreate(backupRootPath);
            }

            if (backupRoot != null && SecurePath.IsContained(settingsRoot.FinalPath, backupRoot.FinalPath))
            {
                throw new IOException("The backup root cannot be inside the settings root.");
            }

            string? latestArchive = backupRoot == null ? null : GetLatestArchiveFileName(backupRoot);
            IReadOnlyDictionary<string, string> previous = new SortedDictionary<string, string>(WindowsPathComparer.Instance);
            if (latestArchive != null)
            {
                stagingParent = SecureDirectoryRoot.Open(stagingParentPath);
                previousStaging = BackupRestoreArchive.ExtractToExclusiveStaging(backupRoot!, latestArchive, stagingParent);
                previous = ReadSettings(previousStaging);
            }

            List<string> updated = current
                .Where(item => !previous.TryGetValue(item.Key, out string? value) ||
                    !JsonEquivalent(item.Value, value, ignoreArrayOrder: policy.GetRestoreMode(item.Key) == RestoreMode.Merge))
                .Select(item => item.Key)
                .ToList();
            if (updated.Count == 0)
            {
                return result = new BackupOperationResult(false, latestArchive != null, latestArchive);
            }

            if (dryRun)
            {
                return result = new BackupOperationResult(true, latestArchive != null, latestArchive);
            }

            string archiveName = CreateArchive(
                backupRoot!,
                current,
                updated,
                productVersion,
                machineName,
                utcNow);
            Exception? retentionError = null;
            try
            {
                RemoveOldArchives(backupRoot!, utcNow.Subtract(TimeSpan.FromDays(60)), 10);
            }
            catch (Exception exception)
            {
                retentionError = exception;
            }

            return result = new BackupOperationResult(true, latestArchive != null, archiveName, retentionError);
        }
        catch (Exception exception)
        {
            operationError = exception;
            throw;
        }
        finally
        {
            Exception? cleanupError = CleanupStaging(previousStaging);
            if (operationError != null && cleanupError != null)
            {
                operationError.Data["StagingCleanupError"] = cleanupError;
            }
            else if (result != null)
            {
                result.StagingCleanupError = cleanupError;
            }

            stagingParent?.Dispose();
            backupRoot?.Dispose();
        }
    }

    /// <summary>
    /// Restores included settings from the newest validated archive.
    /// Rollback can leave empty settings directories rather than deleting directories by pathname.
    /// </summary>
    public RestoreOperationResult Restore(
        string settingsRootPath,
        string backupRootPath,
        string stagingParentPath)
    {
        using SecureDirectoryRoot settingsRoot = SecureDirectoryRoot.Open(settingsRootPath);
        using SecureDirectoryRoot backupRoot = SecureDirectoryRoot.OpenReadOnly(backupRootPath);
        using SecureDirectoryRoot stagingParent = SecureDirectoryRoot.Open(stagingParentPath);
        string archiveName = GetLatestArchiveFileName(backupRoot) ?? throw new FileNotFoundException("No settings backup was found.");
        SecureDirectoryRoot? staging = null;
        RestoreOperationResult? result = null;
        Exception? operationError = null;
        try
        {
            using SecureFile archiveFile = backupRoot.OpenFileForRead(archiveName);
            staging = BackupRestoreArchive.ExtractToExclusiveStaging(archiveFile, stagingParent);
            SortedSet<string> currentPaths = new(settingsRoot.EnumerateFiles(fileFilter: IsIncludedJson), WindowsPathComparer.Instance);
            List<RestoreWriteOperation> operations = [];
            try
            {
                foreach (string relativePath in staging.EnumerateFiles(fileFilter: IsJsonOrManifest))
                {
                    if (relativePath.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || !policy.ShouldInclude(relativePath))
                    {
                        continue;
                    }

                    using SecureFile backupFile = staging.OpenFileForRead(relativePath);
                    string restoreJson = policy.CreateExportVersion(relativePath, backupFile.ReadAllText());
                    if (currentPaths.Contains(relativePath))
                    {
                        SecureFile currentFile = settingsRoot.OpenFileForOverwrite(relativePath);
                        try
                        {
                            string originalJson = currentFile.ReadAllText();
                            string currentJson = policy.CreateExportVersion(relativePath, originalJson);
                            RestoreMode restoreMode = policy.GetRestoreMode(relativePath);
                            if (JsonEquivalent(currentJson, restoreJson, ignoreArrayOrder: restoreMode == RestoreMode.Merge))
                            {
                                currentFile.Dispose();
                                continue;
                            }

                            string contents = restoreMode == RestoreMode.Overwrite
                                ? restoreJson
                                : JsonSettingsMerge.Merge(originalJson, restoreJson);
                            if (JsonEquivalent(originalJson, contents))
                            {
                                currentFile.Dispose();
                                continue;
                            }

                            operations.Add(new RestoreWriteOperation(relativePath, contents, originalJson, currentFile, isNew: false));
                        }
                        catch
                        {
                            currentFile.Dispose();
                            throw;
                        }
                    }
                    else
                    {
                        operations.Add(new RestoreWriteOperation(relativePath, restoreJson, originalContents: null, targetFile: null, isNew: true));
                    }
                }
            }
            catch
            {
                foreach (RestoreWriteOperation operation in operations)
                {
                    operation.Dispose();
                }

                throw;
            }

            if (operations.Count == 0)
            {
                return result = new RestoreOperationResult(false, false);
            }

            ApplyRestoreOperations(settingsRoot, operations);
            return result = new RestoreOperationResult(true, policy.RestartAfterRestore);
        }
        catch (Exception exception)
        {
            operationError = exception;
            throw;
        }
        finally
        {
            Exception? cleanupError = CleanupStaging(staging);
            if (operationError != null && cleanupError != null)
            {
                operationError.Data["StagingCleanupError"] = cleanupError;
            }
            else if (result != null)
            {
                result.StagingCleanupError = cleanupError;
            }
        }
    }

    /// <summary>
    /// Validates the newest archive and reads only its manifest through the opened handle.
    /// </summary>
    public JsonNode? GetLatestManifest(string backupRootPath)
    {
        if (!Directory.Exists(backupRootPath))
        {
            return null;
        }

        using SecureDirectoryRoot backupRoot = SecureDirectoryRoot.OpenReadOnly(backupRootPath);
        string? archiveName = GetLatestArchiveFileName(backupRoot);
        if (archiveName == null)
        {
            return null;
        }

        using SecureFile archiveFile = backupRoot.OpenFileForRead(archiveName);
        using ZipArchive archive = new(archiveFile.Stream, ZipArchiveMode.Read, leaveOpen: true);
        IReadOnlyList<ArchiveEntryDescriptor> descriptors = BackupRestoreArchive.Validate(archive);
        for (int index = 0; index < descriptors.Count; index++)
        {
            ArchiveEntryDescriptor descriptor = descriptors[index];
            if (!descriptor.IsDirectory && WindowsPathComparer.Instance.EqualsPath(descriptor.RelativePath, "manifest.json"))
            {
                using MemoryStream contents = new();
                BackupRestoreArchive.CopyValidatedEntry(archive.Entries[index], descriptor, contents);
                contents.Position = 0;
                using StreamReader reader = new(contents);
                return JsonNode.Parse(reader.ReadToEnd());
            }
        }

        throw new FileNotFoundException("The settings backup manifest could not be found.");
    }

    /// <summary>
    /// Gets the newest archive candidate name; the selected file is validated when its contents are opened.
    /// </summary>
    public static string? GetLatestArchiveFileName(string backupRootPath)
    {
        if (!Directory.Exists(backupRootPath))
        {
            return null;
        }

        using SecureDirectoryRoot root = SecureDirectoryRoot.OpenReadOnly(backupRootPath);
        return GetLatestArchiveFileName(root);
    }

    private static string? GetLatestArchiveFileName(SecureDirectoryRoot backupRoot)
    {
        return backupRoot.EnumerateTopLevelFileNames()
            .Select(path => (Path: path, Timestamp: ParseArchiveTimestamp(path)))
            .Where(item => item.Timestamp.HasValue && Path.GetDirectoryName(item.Path) == string.Empty)
            .OrderByDescending(item => item.Timestamp)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    private IReadOnlyDictionary<string, string> ReadSettings(SecureDirectoryRoot root)
    {
        SortedDictionary<string, string> settings = new(WindowsPathComparer.Instance);
        foreach (string relativePath in root.EnumerateFiles(fileFilter: IsIncludedJson))
        {
            using SecureFile file = root.OpenFileForRead(relativePath);
            settings.Add(relativePath, policy.CreateExportVersion(relativePath, file.ReadAllText()));
        }

        return settings;
    }

    private static string CreateArchive(
        SecureDirectoryRoot backupRoot,
        IReadOnlyDictionary<string, string> settings,
        IReadOnlyCollection<string> updated,
        string productVersion,
        string machineName,
        DateTime utcNow)
    {
        long timestamp = utcNow.ToFileTimeUtc();
        SortedSet<string> updatedPaths = new(updated, WindowsPathComparer.Instance);
        for (int attempt = 0; attempt < 32; attempt++)
        {
            string archiveName = $"settings_{timestamp + attempt}.ptb";
            SecureFile? archiveFile = null;
            try
            {
                archiveFile = backupRoot.CreateNewFile(archiveName);
                using (ZipArchive archive = new(archiveFile.Stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach ((string path, string contents) in settings.OrderBy(item => item.Key, WindowsPathComparer.Instance))
                    {
                        WriteEntry(archive, path, contents);
                    }

                    string manifest = JsonSerializer.Serialize(
                        new
                        {
                            CreateDateTime = utcNow.ToString("u", CultureInfo.InvariantCulture),
                            Version = productVersion,
                            UpdatedFiles = updated.Select(AddLegacyManifestPrefix).ToList(),
                            BackupSource = machineName,
                            UnchangedFiles = settings.Keys
                                .Where(path => !updatedPaths.Contains(path))
                                .Select(AddLegacyManifestPrefix)
                                .ToList(),
                        },
                        ManifestSerializerOptions);
                    WriteEntry(archive, "manifest.json", manifest);
                }

                archiveFile.Stream.Flush(flushToDisk: true);
                archiveFile.Stream.Position = 0;
                using (ZipArchive archive = new(archiveFile.Stream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    BackupRestoreArchive.Validate(archive);
                }

                return archiveName;
            }
            catch (Win32Exception exception) when (archiveFile == null && exception.NativeErrorCode is 80 or 183)
            {
                continue;
            }
            catch (Exception archiveException)
            {
                if (archiveFile != null)
                {
                    try
                    {
                        archiveFile.MarkForDeletion();
                    }
                    catch (Exception cleanupException)
                    {
                        archiveException.Data["ArchiveCleanupError"] = cleanupException;
                    }
                }

                throw;
            }
            finally
            {
                archiveFile?.Dispose();
            }
        }

        throw new IOException("Could not create a unique settings backup archive.");
    }

    private static void WriteEntry(ZipArchive archive, string path, string contents)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path.Replace('\\', '/'), CompressionLevel.Optimal);
        using StreamWriter writer = new(entry.Open());
        writer.Write(contents);
    }

    private static void RemoveOldArchives(SecureDirectoryRoot backupRoot, DateTime deleteBefore, int minimumToKeep)
    {
        List<(string Path, long Timestamp)> archives = backupRoot.EnumerateTopLevelFileNames()
            .Select(path => (Path: path, Timestamp: ParseArchiveTimestamp(path)))
            .Where(item => item.Timestamp.HasValue && Path.GetDirectoryName(item.Path) == string.Empty)
            .Select(item => (item.Path, item.Timestamp!.Value))
            .OrderByDescending(item => item.Value)
            .ToList();
        foreach ((string path, long timestamp) in archives.Skip(minimumToKeep))
        {
            if (DateTime.FromFileTimeUtc(timestamp) < deleteBefore)
            {
                backupRoot.DeleteEntry(path, isDirectory: false);
            }
        }
    }

    private static string AddLegacyManifestPrefix(string path)
    {
        return path.StartsWith('\\') ? path : "\\" + path;
    }

    private bool IsIncludedJson(string path)
    {
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && policy.ShouldInclude(path);
    }

    private static bool IsJsonOrManifest(string path)
    {
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static long? ParseArchiveTimestamp(string path)
    {
        string fileName = Path.GetFileName(path);
        if (!fileName.StartsWith("settings_", StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(".ptb", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string value = fileName["settings_".Length..^".ptb".Length];
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long timestamp) ? timestamp : null;
    }

    private static bool JsonEquivalent(string left, string right, bool ignoreArrayOrder = false)
    {
        JsonNode? leftNode = JsonNode.Parse(left);
        JsonNode? rightNode = JsonNode.Parse(right);
        if (ignoreArrayOrder)
        {
            leftNode = SortJsonForComparison(leftNode);
            rightNode = SortJsonForComparison(rightNode);
        }

        return JsonNode.DeepEquals(leftNode, rightNode);
    }

    private static JsonNode? SortJsonForComparison(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            return new JsonObject(jsonObject
                .OrderBy(property => property.Key, StringComparer.Ordinal)
                .Select(property => KeyValuePair.Create(property.Key, SortJsonForComparison(property.Value))));
        }

        if (node is JsonArray jsonArray)
        {
            return new JsonArray(jsonArray
                .Select(SortJsonForComparison)
                .OrderBy(item => item?.ToJsonString(), StringComparer.Ordinal)
                .ToArray());
        }

        return node?.DeepClone();
    }

    private Exception? CleanupStaging(SecureDirectoryRoot? staging)
    {
        if (staging == null)
        {
            return null;
        }

        try
        {
            try
            {
                BeforeStagingCleanup?.Invoke(staging);
                staging.DeleteTree();
            }
            finally
            {
                staging.Dispose();
            }

            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private void ApplyRestoreOperations(SecureDirectoryRoot settingsRoot, List<RestoreWriteOperation> operations)
    {
        List<RestoreWriteOperation> overwritten = [];
        try
        {
            foreach (RestoreWriteOperation operation in operations.Where(operation => operation.IsNew))
            {
                BeforeRestoreFileCreate?.Invoke(operation.RelativePath);
                operation.TargetFile = settingsRoot.CreateNewFile(operation.RelativePath);
            }

            foreach (RestoreWriteOperation operation in operations)
            {
                if (!operation.IsNew)
                {
                    overwritten.Add(operation);
                }

                operation.TargetFile!.OverwriteAllText(operation.NewContents);
                AfterRestoreWrite?.Invoke(operation.RelativePath);
            }
        }
        catch (Exception restoreException)
        {
            List<Exception> rollbackErrors = [];
            for (int index = overwritten.Count - 1; index >= 0; index--)
            {
                TryRollback(
                    () => overwritten[index].TargetFile!.OverwriteAllText(overwritten[index].OriginalContents!),
                    rollbackErrors);
            }

            foreach (RestoreWriteOperation operation in operations.Where(operation => operation.IsNew))
            {
                if (operation.TargetFile != null)
                {
                    TryRollback(operation.TargetFile.MarkForDeletion, rollbackErrors);
                    TryRollback(
                        () =>
                        {
                            operation.Dispose();
                            AfterRestoreRollbackFileClosed?.Invoke(operation.RelativePath);
                        },
                        rollbackErrors);
                }
            }

            if (rollbackErrors.Count > 0)
            {
                restoreException.Data["RestoreRollbackErrors"] = rollbackErrors.ToArray();
            }

            throw;
        }
        finally
        {
            foreach (RestoreWriteOperation operation in operations)
            {
                operation.Dispose();
            }
        }
    }

    private static void TryRollback(Action rollback, List<Exception> rollbackErrors)
    {
        try
        {
            rollback();
        }
        catch (Exception exception)
        {
            rollbackErrors.Add(exception);
        }
    }

    private sealed class RestoreWriteOperation : IDisposable
    {
        internal RestoreWriteOperation(
            string relativePath,
            string newContents,
            string? originalContents,
            SecureFile? targetFile,
            bool isNew)
        {
            RelativePath = relativePath;
            NewContents = newContents;
            OriginalContents = originalContents;
            TargetFile = targetFile;
            IsNew = isNew;
        }

        internal string RelativePath { get; }

        internal string NewContents { get; }

        internal string? OriginalContents { get; }

        internal SecureFile? TargetFile { get; set; }

        internal bool IsNew { get; }

        public void Dispose()
        {
            TargetFile?.Dispose();
            TargetFile = null;
        }
    }
}
