// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Awake.LeanProbe;

internal sealed class ProbeSettings
{
    [JsonPropertyName("properties")]
    public ProbeProperties Properties { get; set; } = new();
}
