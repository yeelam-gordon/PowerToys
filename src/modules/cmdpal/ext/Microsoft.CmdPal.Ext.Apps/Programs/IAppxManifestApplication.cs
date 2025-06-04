// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("5DA89BF4-3773-46BE-B650-7E744863B7E8")]
public partial interface IAppxManifestApplication
{
    HRESULT GetStringValue([In, MarshalAs(UnmanagedType.LPWStr)] string name, [Out, MarshalAs(UnmanagedType.LPWStr)] out string value);

    HRESULT GetAppUserModelId([Out, MarshalAs(UnmanagedType.LPWStr)] out string appUserModelId);
}
