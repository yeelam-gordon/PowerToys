// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Native;

/// <summary>
/// AOT-compatible Win32 API imports
/// </summary>
internal static partial class Win32Apis
{
    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);

    [LibraryImport("shell32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShellExecuteExW(ref ShellExecuteInfo lpExecInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct ShellExecuteInfo
    {
        public uint cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpFile;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public IntPtr lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    // CLSCTX constants
    public const uint CLSCTX_INPROC_SERVER = 0x1;
}

/// <summary>
/// ComWrappers implementation for AOT-compatible COM interop
/// </summary>
internal sealed class IndexerComWrappers : ComWrappers
{
    protected override ComInterfaceEntry* ComputeVtables(object obj, CreateComInterfaceFlags flags, out int count)
    {
        // For this implementation, we only need to handle COM object creation
        // We don't need to expose managed objects as COM objects
        count = 0;
        return null;
    }

    protected override object? CreateObject(IntPtr externalComObject, CreateObjectFlags flags)
    {
        // This method is called when we need to create a managed wrapper for a COM object
        // For our use case with Search API, we'll let the runtime handle this
        return null;
    }

    protected override void ReleaseObjects(IEnumerable objects)
    {
        // Release any held references - default implementation should suffice
        throw new NotImplementedException();
    }
}