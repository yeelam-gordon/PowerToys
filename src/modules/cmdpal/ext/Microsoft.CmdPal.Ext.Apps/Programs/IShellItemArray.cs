// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
public partial interface IShellItemArray
{
    HRESULT BindToHandler([In] nint pbc, [In] Guid bhid, [In] Guid riid, [Out] out nint ppvOut);

    HRESULT GetPropertyStore([In] uint flags, [In] Guid riid, [Out] out nint ppv);

    HRESULT GetPropertyDescriptionList([In] nint keyType, [In] Guid riid, [Out] out nint ppv);

    HRESULT GetAttributes([In] uint AttribFlags, [In] uint sfgaoMask, [Out] out uint psfgaoAttribs);

    HRESULT GetCount([Out] out uint pdwNumItems);

    HRESULT GetItemAt([In] uint dwIndex, [Out] out IShellItem ppsi);

    HRESULT EnumItems([Out] out nint ppenumShellItems);
}