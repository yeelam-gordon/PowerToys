// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("BEB94909-E451-438B-B5A7-D79E767B75D8")]
public partial interface IAppxFactory
{
    HRESULT CreatePackageWriter([In] IStream outputStream, [In] nint settings, [Out] out nint packageWriter);

    HRESULT CreatePackageReader([In] IStream inputStream, [Out] out nint packageReader);

    HRESULT CreateManifestReader([In] IStream inputStream, [Out] out IAppxManifestReader manifestReader);
}
