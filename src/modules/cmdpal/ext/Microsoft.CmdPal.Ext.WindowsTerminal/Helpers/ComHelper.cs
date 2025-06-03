// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public static class ComHelper
{
    public static IApplicationActivationManager CreateApplicationActivationManager()
    {
        var cw = new StrategyBasedComWrappers();
        
        unsafe
        {
            var hr = PInvoke.CoCreateInstance(
                PInvoke.CLSID_ApplicationActivationManager,
                null,
                CLSCTX.CLSCTX_INPROC_SERVER,
                typeof(IApplicationActivationManager).GUID,
                out var obj);

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return (IApplicationActivationManager)cw.GetOrCreateObjectForComInstance((nint)obj, CreateObjectFlags.None);
        }
    }
}