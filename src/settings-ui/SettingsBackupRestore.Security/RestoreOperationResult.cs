// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Result of a production restore.
/// </summary>
public sealed record RestoreOperationResult(bool SettingsChanged, bool RestartRequired)
{
    /// <summary>
    /// Gets a temporary-file cleanup failure reported separately from the completed operation.
    /// </summary>
    public Exception? StagingCleanupError { get; internal set; }
}
