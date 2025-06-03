// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

// Reference : https://github.com/MicrosoftEdge/edge-launcher/blob/108e63df0b4cb5cd9d5e45aa7a264690851ec51d/MIcrosoftEdgeLauncherCsharp/Program.cs
[Flags]
public enum ActivateOptions
{
    None = 0x00000000,
    DesignMode = 0x00000001,
    NoErrorUI = 0x00000002,
    NoSplashScreen = 0x00000004,
}

// ApplicationActivationManager
[GeneratedComInterface]
[Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
public partial interface IApplicationActivationManager
{
    nint ActivateApplication([In] string appUserModelId, [In] string arguments, [In] ActivateOptions options, [Out] out uint processId);

    nint ActivateForFile([In] string appUserModelId, [In] nint /*IShellItemArray* */ itemArray, [In] string verb, [Out] out uint processId);

    nint ActivateForProtocol([In] string appUserModelId, [In] nint /* IShellItemArray* */itemArray, [Out] out uint processId);
}
