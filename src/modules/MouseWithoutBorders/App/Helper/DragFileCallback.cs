// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace MouseWithoutBorders;

internal static class DragFileCallback
{
    internal static bool TryReadFile(ref long pendingGeneration, Func<object?> readData, [NotNullWhen(true)] out string? fileName, out long generation)
    {
        fileName = null;
        generation = Interlocked.Read(ref pendingGeneration);
        if (generation == 0)
        {
            return false;
        }

        object? data = readData();
        if (data == null)
        {
            return false;
        }

        string[] files = (string[])data;
        if (files.Length == 0
            || Interlocked.CompareExchange(ref pendingGeneration, 0, generation) != generation)
        {
            return false;
        }

        fileName = files[0].ToString();
        return true;
    }
}
