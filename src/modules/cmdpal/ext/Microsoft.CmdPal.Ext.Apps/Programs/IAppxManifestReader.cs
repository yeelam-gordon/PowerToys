// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("4E1BD148-55A0-4480-A3D1-15544710637C")]
public partial interface IAppxManifestReader
{
    HRESULT GetPackageId([Out] out nint packageId);

    HRESULT GetProperties([Out] out IAppxManifestProperties packageProperties);

    HRESULT GetPackageDependencies([Out] out nint dependencies);

    HRESULT GetCapabilities([Out] out nint capabilities);

    HRESULT GetResources([Out] out nint resources);

    HRESULT GetDeviceCapabilities([Out] out nint deviceCapabilities);

    HRESULT GetPrerequisite([In] string name, [Out] out ulong value);

    HRESULT GetApplications([Out] out IAppxManifestApplicationsEnumerator applications);

    HRESULT GetStream([Out] out nint manifestStream);
}
