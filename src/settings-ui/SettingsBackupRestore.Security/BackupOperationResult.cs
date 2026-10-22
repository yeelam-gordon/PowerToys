// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Result of a production backup comparison or write.
/// </summary>
public sealed record BackupOperationResult(bool BackupCreated, bool PreviousBackupExists, string? ArchiveFileName, Exception? RetentionError = null);
