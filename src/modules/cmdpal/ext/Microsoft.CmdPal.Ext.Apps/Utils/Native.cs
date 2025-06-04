// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

[SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "We want plugins to share this NativeMethods class, instead of each one creating its own.")]
public sealed class Native
{
    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, nint ppvReserved);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string path, nint pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial HRESULT SHCreateStreamOnFileEx(string fileName, STGM grfMode, uint attributes, [MarshalAs(UnmanagedType.Bool)] bool create, System.Runtime.InteropServices.ComTypes.IStream reserved, out System.Runtime.InteropServices.ComTypes.IStream stream);

    [LibraryImport("ole32.dll")]
    public static partial HRESULT CoCreateInstance(
        [In] ref Guid rclsid,
        [In] IntPtr pUnkOuter,
        [In] CLSCTX dwClsContext,
        [In] ref Guid riid,
        [Out] out IntPtr ppv);

    [LibraryImport("ole32.dll")]
    public static partial void CoTaskMemFree(IntPtr pv);

    public static class ShellItemTypeConstants
    {
        /// <summary>
        /// Guid for type IShellItem.
        /// </summary>
        public static readonly Guid ShellItemGuid = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

        /// <summary>
        /// Guid for type IShellItem2.
        /// </summary>
        public static readonly Guid ShellItem2Guid = new("7E9FB0D3-919F-4307-AB2E-9B1860310C93");
    }

    /// <summary>
    /// The following are ShellItem DisplayName types.
    /// </summary>
    [Flags]
    public enum SIGDN : uint
    {
        NORMALDISPLAY = 0,
        PARENTRELATIVEPARSING = 0x80018001,
        PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
        DESKTOPABSOLUTEPARSING = 0x80028000,
        PARENTRELATIVEEDITING = 0x80031001,
        DESKTOPABSOLUTEEDITING = 0x8004c000,
        FILESYSPATH = 0x80058000,
        URL = 0x80068000,
    }

    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    public partial interface IShellItem
    {
        HRESULT BindToHandler(
            nint pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out nint ppv);

        HRESULT GetParent(out IShellItem ppsi);

        HRESULT GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

        HRESULT GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

        HRESULT Compare(IShellItem psi, uint hint, out int piOrder);
    }

    /// <summary>
    /// <see href="https://learn.microsoft.com/windows/win32/stg/stgm-constants">see all STGM values</see>
    /// </summary>
    [Flags]
    public enum STGM : long
    {
        READ = 0x00000000L,
        WRITE = 0x00000001L,
        READWRITE = 0x00000002L,
        CREATE = 0x00001000L,
    }
}
