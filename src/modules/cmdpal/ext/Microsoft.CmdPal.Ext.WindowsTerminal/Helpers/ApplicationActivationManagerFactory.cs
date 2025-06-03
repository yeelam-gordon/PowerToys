// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public static class ApplicationActivationManagerFactory
{
    // ApplicationActivationManager CLSID
    private static readonly Guid ApplicationActivationManagerClsid = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C");
    
    public static IApplicationActivationManager CreateInstance()
    {
        var comWrappers = new StrategyBasedComWrappers();
        
        // Use Type.GetTypeFromCLSID to create the COM object
        var type = Type.GetTypeFromCLSID(ApplicationActivationManagerClsid);
        if (type == null)
        {
            throw new InvalidOperationException("Failed to get COM type for ApplicationActivationManager");
        }
        
        var comObject = Activator.CreateInstance(type);
        if (comObject == null)
        {
            throw new InvalidOperationException("Failed to create ApplicationActivationManager instance");
        }
        
        // Use ComWrappers to get the managed interface
        var pUnk = Marshal.GetIUnknownForObject(comObject);
        try
        {
            var result = comWrappers.GetOrCreateObjectForComInstance(pUnk, CreateObjectFlags.None);
            return (IApplicationActivationManager)result;
        }
        finally
        {
            Marshal.Release(pUnk);
        }
    }
}