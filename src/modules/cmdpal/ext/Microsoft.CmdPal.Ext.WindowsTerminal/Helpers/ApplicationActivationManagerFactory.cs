// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

/// <summary>
/// Factory for creating IApplicationActivationManager instances using AOT-compatible APIs
/// </summary>
internal static class ApplicationActivationManagerFactory
{
    private static readonly Guid CLSID_ApplicationActivationManager = new("45BA127D-10A8-46EA-8AB7-56EA9078943C");

    public static IApplicationActivationManager CreateInstance()
    {
        unsafe
        {
            void* pUnknown;
            PInvoke.CoCreateInstance(
                CLSID_ApplicationActivationManager,
                null,
                CLSCTX.CLSCTX_LOCAL_SERVER,
                typeof(IApplicationActivationManager).GUID,
                &pUnknown).ThrowOnFailure();

            return (IApplicationActivationManager)Marshal.GetObjectForIUnknown((IntPtr)pUnknown);
        }
    }
}