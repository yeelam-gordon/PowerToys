// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("9EB8A55A-F04B-4D0D-808D-686185D4847A")]
public partial interface IAppxManifestApplicationsEnumerator
{
    HRESULT GetCurrent([Out] out IAppxManifestApplication application);

    HRESULT GetHasCurrent([Out] out bool hasCurrent);

    HRESULT MoveNext([Out] out bool hasNext);
}
