// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

// ApplicationActivationManager
[GeneratedComInterface]
[Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
public partial interface IApplicationActivationManager
{
    int ActivateApplication([In] [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [In] [MarshalAs(UnmanagedType.LPWStr)] string? arguments, [In] ACTIVATEOPTIONS options, [Out] out uint processId);

    int ActivateForFile([In] [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [In] IShellItemArray itemArray, [In] [MarshalAs(UnmanagedType.LPWStr)] string? verb, [Out] out uint processId);

    int ActivateForProtocol([In] [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [In] IShellItemArray itemArray, [Out] out uint processId);
}
