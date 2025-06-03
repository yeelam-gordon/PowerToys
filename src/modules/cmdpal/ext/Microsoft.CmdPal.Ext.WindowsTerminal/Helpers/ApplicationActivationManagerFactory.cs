// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public static class ApplicationActivationManagerFactory
{
    // ApplicationActivationManager CLSID
    private static readonly Guid ApplicationActivationManagerClsid = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C");
    
    public static IApplicationActivationManager CreateInstance()
    {
        // Use PInvoke.CoCreateInstance to create the COM object (AOT compatible)
        var hr = PInvoke.CoCreateInstance(
            ApplicationActivationManagerClsid, 
            null, 
            CLSCTX.CLSCTX_ALL, 
            typeof(IApplicationActivationManager).GUID, 
            out var comObject);
            
        Marshal.ThrowExceptionForHR(hr);
        
        if (comObject == null)
        {
            throw new InvalidOperationException("Failed to create ApplicationActivationManager instance");
        }
        
        return (IApplicationActivationManager)comObject;
    }
}