// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

/// <summary>
/// IShellItem interface with GeneratedComInterface for AOT compatibility
/// </summary>
[GeneratedComInterface]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
public partial interface IShellItem
{
    void BindToHandler(
        nint pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out nint ppv);

    void GetParent(out IShellItem ppsi);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetDisplayName(SIGDN sigdnName);

    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    void Compare(IShellItem psi, uint hint, out int piOrder);
}

/// <summary>
/// Constants and helper types for Shell operations
/// </summary>
public static class ShellConstants
{
    /// <summary>
    /// Guid for type IShellItem.
    /// </summary>
    public static readonly Guid ShellItemGuid = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    /// <summary>
    /// Guid for type IShellItem2.
    /// </summary>
    public static readonly Guid ShellItem2Guid = new("7E9FB0D3-919F-4307-AB2E-9B1860310C93");
}
}
