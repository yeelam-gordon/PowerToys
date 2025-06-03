// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Interop;

internal static partial class ComApi
{
    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out IntPtr ppv);

    [LibraryImport("oleaut32.dll")]
    internal static partial int GetErrorInfo(
        uint dwReserved,
        out IntPtr pperrinfo);
}

internal static partial class ShellApi
{
    [LibraryImport("shell32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShellExecuteExW(ref IntPtr lpExecInfo);
}

internal static class ClsContext
{
    public const uint CLSCTX_INPROC_SERVER = 0x1;
    public const uint CLSCTX_LOCAL_SERVER = 0x4;
    public const uint CLSCTX_REMOTE_SERVER = 0x10;
}