// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public static class AppxPackageHelper
{
    // This function returns a list of attributes of applications
    internal static IEnumerable<IAppxManifestApplication> GetAppsFromManifest(IStream stream)
    {
        var appxFactory = ComHelper.CreateInstance<IAppxFactory>(AppxFactoryConstants.CLSID, typeof(IAppxFactory).GUID);
        var reader = appxFactory.CreateManifestReader(stream);
        var manifestApps = reader.GetApplications();

        while (manifestApps.GetHasCurrent())
        {
            var manifestApp = manifestApps.GetCurrent();
            var hr2 = manifestApp.GetStringValue("AppListEntry", out var appListEntry);
            _ = CheckHRAndReturnOrThrow(hr2, appListEntry);
            if (appListEntry != "none")
            {
                yield return manifestApp;
            }

            manifestApps.MoveNext();
        }
    }

    internal static T CheckHRAndReturnOrThrow<T>(HRESULT hr, T result)
    {
        if (hr.Failed)
        {
            Marshal.ThrowExceptionForHR((int)hr);
        }

        return result;
    }
}
