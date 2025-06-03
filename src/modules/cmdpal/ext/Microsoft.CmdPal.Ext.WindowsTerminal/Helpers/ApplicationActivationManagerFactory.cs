// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public static class ApplicationActivationManagerFactory
{
    // ApplicationActivationManager CLSID
    private static readonly Guid ApplicationActivationManagerClsid = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C");
    
    // IApplicationActivationManager IID
    private static readonly Guid IApplicationActivationManagerIid = new Guid("2e941141-7f97-4756-ba1d-9decde894a3d");
    
    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out IntPtr ppv);
    
    private const uint CLSCTX_ALL = 0x00000017;
    
    public static IApplicationActivationManager CreateInstance()
    {
        // Use LibraryImport CoCreateInstance to create the COM object (AOT compatible)
        var hr = CoCreateInstance(
            ApplicationActivationManagerClsid, 
            IntPtr.Zero, 
            CLSCTX_ALL, 
            IApplicationActivationManagerIid, 
            out var pUnknown);
            
        Marshal.ThrowExceptionForHR(hr);
        
        if (pUnknown == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create ApplicationActivationManager instance");
        }
        
        // Use StrategyBasedComWrappers to wrap the COM object
        var comWrappers = new StrategyBasedComWrappers();
        var managedObject = comWrappers.GetOrCreateObjectForComInstance(pUnknown, CreateObjectFlags.None);
        
        // Release the native reference since GetOrCreateObjectForComInstance adds its own reference
        Marshal.Release(pUnknown);
        
        return (IApplicationActivationManager)managedObject;
    }
}