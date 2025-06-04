// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using static Microsoft.CmdPal.Ext.Apps.Utils.Native;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public static class AppxPackageHelper
{
    private static readonly IAppxFactory AppxFactory = GetAppxFactory();

    private static IAppxFactory GetAppxFactory()
    {
        var cw = new StrategyBasedComWrappers();
        
        var clsid = AppxFactoryClsid.CLSID_AppxFactory;
        var iid = typeof(IAppxFactory).GUID;
        var hr = CoCreateInstance(
            ref clsid,
            nint.Zero,
            CLSCTX.CLSCTX_INPROC_SERVER,
            ref iid,
            out var comInstance);
            
        if (hr.Failed)
        {
            return null;
        }
        
        return cw.GetOrCreateObjectForComInstance(comInstance, CreateObjectFlags.None) as IAppxFactory;
    }

    // This function returns a list of attributes of applications
    internal static IEnumerable<IAppxManifestApplication> GetAppsFromManifest(IStream stream)
    {
        var hr = AppxFactory.CreateManifestReader(stream, out var reader);
        if (hr.Failed || reader == null)
        {
            yield break;
        }

        hr = reader.GetApplications(out var manifestApps);
        if (hr.Failed || manifestApps == null)
        {
            yield break;
        }

        hr = manifestApps.GetHasCurrent(out var hasCurrent);
        while (hr.Succeeded && hasCurrent)
        {
            hr = manifestApps.GetCurrent(out var manifestApp);
            if (hr.Failed || manifestApp == null)
            {
                break;
            }

            var appListEntryHr = manifestApp.GetStringValue("AppListEntry", out var appListEntry);
            _ = CheckHRAndReturnOrThrow(appListEntryHr, appListEntry);
            if (appListEntry != "none")
            {
                yield return manifestApp;
            }

            hr = manifestApps.MoveNext(out var hasNext);
            hasCurrent = hasNext;
        }
    }

    internal static T CheckHRAndReturnOrThrow<T>(HRESULT hr, T result)
    {
        if (hr != HRESULT.S_OK)
        {
            Marshal.ThrowExceptionForHR((int)hr);
        }

        return result;
    }
}
