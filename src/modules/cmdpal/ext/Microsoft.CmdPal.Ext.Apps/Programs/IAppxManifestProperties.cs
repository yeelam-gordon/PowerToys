// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static Microsoft.CmdPal.Ext.Apps.Utils.Native;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("03FAF64D-F26F-4B2C-AAF7-8FE7789B8BCA")]
public partial interface IAppxManifestProperties
{
    [PreserveSig]
    HRESULT GetBoolValue([MarshalAs(UnmanagedType.LPWStr)] string name, out bool value);

    [PreserveSig]
    HRESULT GetStringValue([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] out string value);
}
