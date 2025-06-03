// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

[SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "We want plugins to share this NativeMethods class, instead of each one creating its own.")]
public sealed class Native
{
    // Use the Windows.Win32 versions of these APIs instead of manual P/Invoke
    public static int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, nint ppvReserved)
    {
        return (int)PInvoke.SHLoadIndirectString(pszSource, pszOutBuf, (uint)cchOutBuf, ppvReserved);
    }

    public static int SHCreateItemFromParsingName(string path, nint pbc, ref Guid riid, out IShellItem shellItem)
    {
        var hr = PInvoke.SHCreateItemFromParsingName(path, null, riid, out var item);
        shellItem = (IShellItem)item;
        return (int)hr;
    }

    public static HRESULT SHCreateStreamOnFileEx(string fileName, STGM grfMode, uint attributes, bool create, System.Runtime.InteropServices.ComTypes.IStream reserved, out System.Runtime.InteropServices.ComTypes.IStream stream)
    {
        var hr = PInvoke.SHCreateStreamOnFileEx(fileName, (uint)grfMode, attributes, create, null, out var streamPtr);
        stream = (System.Runtime.InteropServices.ComTypes.IStream)streamPtr;
        return hr;
    }

    public static HRESULT SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf, nint ppvReserved)
    {
        return PInvoke.SHLoadIndirectString(pszSource, pszOutBuf, cchOutBuf, ppvReserved);
    }

    // Use SIGDN from Windows.Win32
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
}
