// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[GeneratedComInterface]
[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69")]
public partial interface ISearchManager
{
    void GetIndexerVersionStr([MarshalAs(UnmanagedType.LPWStr)] out string ppszVersionString);

    void GetIndexerVersion(out uint pdwMajor, out uint pdwMinor);

    nint GetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName);

    void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName, ref object pValue);

    string ProxyName
    {
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    string BypassList
    {
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    void SetProxy(
      object sUseProxy,
      int fLocalByPassProxy,
      uint dwPortNumber,
      [MarshalAs(UnmanagedType.LPWStr)] string pszProxyName,
      [MarshalAs(UnmanagedType.LPWStr)] string pszByPassList);

    [return: MarshalAs(UnmanagedType.Interface)]
    ISearchCatalogManager GetCatalog([MarshalAs(UnmanagedType.LPWStr)] string pszCatalog);

    string UserAgent
    {
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
        [param: MarshalAs(UnmanagedType.LPWStr)]
        set;
    }

    object UseProxy
    {
        get;
    }

    int LocalBypass
    {
        get;
    }

    uint PortNumber
    {
        get;
    }
}
