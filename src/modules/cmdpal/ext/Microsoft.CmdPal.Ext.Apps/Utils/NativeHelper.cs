// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.IO;
using Windows.Win32.UI.Shell;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

/// <summary>
/// Native Windows API declarations using LibraryImport for AOT compatibility
/// </summary>
internal static partial class NativeHelper
{
    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial HRESULT SHLoadIndirectString(
        string pszSource,
        [Out] StringBuilder pszOutBuf,
        uint cchOutBuf,
        nint ppvReserved);

    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial HRESULT SHCreateStreamOnFileEx(
        string pszFile,
        uint grfMode,
        uint dwAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool fCreate,
        nint pstmTemplate,
        [MarshalAs(UnmanagedType.Interface)] out IStream ppstm);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial HRESULT SHCreateItemFromParsingName(
        string pszPath,
        nint pbc,
        in Guid riid,
        out nint ppv);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial SafeFileHandle CreateFile(
        string lpFileName,
        FILE_ACCESS_RIGHTS dwDesiredAccess,
        FILE_SHARE_MODE dwShareMode,
        nint lpSecurityAttributes,
        FILE_CREATION_DISPOSITION dwCreationDisposition,
        FILE_FLAGS_AND_ATTRIBUTES dwFlagsAndAttributes,
        nint hTemplateFile);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        nint lpInBuffer,
        uint nInBufferSize,
        nint lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        nint lpOverlapped);

    [LibraryImport("ole32.dll")]
    internal static partial void CoTaskMemFree(nint pv);
}

/// <summary>
/// COM helper using StrategyBasedComWrappers for AOT compatibility
/// </summary>
internal static class ComHelper
{
    private static readonly StrategyBasedComWrappers ComWrappers = new();

    internal static T CreateInstance<T>(Guid clsid, Guid iid, CLSCTX clsContext = CLSCTX.CLSCTX_INPROC_SERVER) where T : class
    {
        var hr = CoCreateInstance(clsid, nint.Zero, clsContext, iid, out var objPtr);
        if (hr.Failed)
        {
            throw new System.ComponentModel.Win32Exception((int)hr, $"Failed to create COM instance for {typeof(T).Name}");
        }

        try
        {
            return (T)ComWrappers.GetOrCreateObjectForComInstance(objPtr, CreateObjectFlags.None);
        }
        finally
        {
            Marshal.Release(objPtr);
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial HRESULT CoCreateInstance(
        in Guid rclsid,
        nint pUnkOuter,
        CLSCTX dwClsContext,
        in Guid riid,
        out nint ppv);
}