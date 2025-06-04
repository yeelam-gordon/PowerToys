// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("03FAF64D-F26F-4B2C-AAF7-8FE7789B8BCA")]
public partial interface IAppxManifestProperties
{
    HRESULT GetBoolValue([In, MarshalAs(UnmanagedType.LPWStr)] string name, [Out] out bool value);

    HRESULT GetStringValue([In, MarshalAs(UnmanagedType.LPWStr)] string name, [Out, MarshalAs(UnmanagedType.LPWStr)] out string value);
}
