// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.Indexer.Interop;

internal static partial class ComApi
{
    [LibraryImport("ole32.dll")]
    internal static partial HRESULT CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        CLSCTX dwClsContext,
        in Guid riid,
        out IntPtr ppv);

    [LibraryImport("oleaut32.dll")]
    internal static partial HRESULT GetErrorInfo(
        uint dwReserved,
        out IntPtr pperrinfo);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShellExecuteExW(ref SHELLEXECUTEINFOW lpExecInfo);
}