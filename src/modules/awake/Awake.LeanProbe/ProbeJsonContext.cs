// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Awake.LeanProbe;

[JsonSerializable(typeof(ProbeSettings))]
internal sealed partial class ProbeJsonContext : JsonSerializerContext
{
}
