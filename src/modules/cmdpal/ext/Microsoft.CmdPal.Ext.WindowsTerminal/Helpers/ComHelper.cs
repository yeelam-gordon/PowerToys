// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public static partial class ComHelper
{
    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(
        in Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object? pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

    public static IApplicationActivationManager CreateApplicationActivationManager()
    {
        ComWrappers cw = new StrategyBasedComWrappers();
        
        var clsid = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"); // CLSID_ApplicationActivationManager
        var iid = typeof(IApplicationActivationManager).GUID;
        
        int hr = CoCreateInstance(in clsid, null, 1 /*CLSCTX_INPROC_SERVER*/, in iid, out object obj);
        if (hr >= 0)
        {
            try
            {
                return (IApplicationActivationManager)cw.GetOrCreateObjectForComInstance((nint)obj, CreateObjectFlags.None);
            }
            finally
            {
                Marshal.Release((nint)obj);
            }
        }
        else
        {
            Marshal.ThrowExceptionForHR(hr);
            return null!; // Never reached
        }
    }
}