// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace Awake.LeanProbe;

internal sealed class ProbeProperties
{
    [JsonPropertyName("mode")]
    public int Mode { get; set; }

    [JsonPropertyName("keepDisplayOn")]
    public bool KeepDisplayOn { get; set; }

    [JsonPropertyName("intervalHours")]
    public uint IntervalHours { get; set; }

    [JsonPropertyName("intervalMinutes")]
    public uint IntervalMinutes { get; set; }

    [JsonPropertyName("expirationDateTime")]
    public DateTimeOffset ExpirationDateTime { get; set; }
}
