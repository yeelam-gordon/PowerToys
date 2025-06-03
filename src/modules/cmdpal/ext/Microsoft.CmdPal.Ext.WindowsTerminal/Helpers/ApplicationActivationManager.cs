// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32.Foundation;
using Windows.Win32;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

// Application Activation Manager Helper
public static class ApplicationActivationManagerHelper
{
    private static readonly Guid CLSID_ApplicationActivationManager = new("45BA127D-10A8-46EA-8AB7-56EA9078943C");
    private static readonly Guid IID_IApplicationActivationManager = new("2e941141-7f97-4756-ba1d-9decde894a3d");

    public static IApplicationActivationManager CreateInstance()
    {
        var comWrappers = new StrategyBasedComWrappers();
        
        var hr = PInvoke.CoCreateInstance(
            CLSID_ApplicationActivationManager,
            null,
            Windows.Win32.System.Com.CLSCTX.CLSCTX_INPROC_SERVER,
            IID_IApplicationActivationManager,
            out var ppv);

        if (hr.Failed)
        {
            throw new COMException("Failed to create ApplicationActivationManager", hr);
        }

        return (IApplicationActivationManager)comWrappers.GetOrCreateObjectForComInstance((nint)ppv, CreateObjectFlags.None);
    }
}
