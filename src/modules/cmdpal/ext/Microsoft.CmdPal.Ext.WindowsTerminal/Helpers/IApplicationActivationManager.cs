// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
public partial interface IApplicationActivationManager
{
    int ActivateApplication(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        [MarshalAs(UnmanagedType.LPWStr)] string arguments,
        Windows.Win32.UI.Shell.ACTIVATEOPTIONS options,
        out uint processId);
        
    int ActivateForFile(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        IntPtr itemArray, // IShellItemArray*
        [MarshalAs(UnmanagedType.LPWStr)] string verb,
        out uint processId);
        
    int ActivateForProtocol(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        IntPtr itemArray, // IShellItemArray*
        out uint processId);
}
