// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

// Reference : https://github.com/MicrosoftEdge/edge-launcher/blob/108e63df0b4cb5cd9d5e45aa7a264690851ec51d/MIcrosoftEdgeLauncherCsharp/Program.cs
[Flags]
public enum ACTIVATEOPTIONS
{
    AO_NONE = 0x00000000,
    AO_DESIGNMODE = 0x00000001,
    AO_NOERRORUI = 0x00000002,
    AO_NOSPLASHSCREEN = 0x00000004,
    AO_PRELAUNCH = 0x02000000,
}

public static class ApplicationActivationManagerClsid
{
    public static readonly Guid CLSID_ApplicationActivationManager = new("45BA127D-10A8-46EA-8AB7-56EA9078943C");
}

// ApplicationActivationManager
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
public partial interface IApplicationActivationManager
{
    HRESULT ActivateApplication([In] string appUserModelId, [In] string arguments, [In] ACTIVATEOPTIONS options, [Out] out uint processId);

    HRESULT ActivateForFile([In] string appUserModelId, [In] IShellItemArray itemArray, [In] string verb, [Out] out uint processId);

    HRESULT ActivateForProtocol([In] string appUserModelId, [In] IShellItemArray itemArray, [Out] out uint processId);
}


