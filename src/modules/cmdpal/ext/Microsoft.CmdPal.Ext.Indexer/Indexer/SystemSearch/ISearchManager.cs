// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69")]
public partial interface ISearchManager
{
    void GetIndexerVersionStr(out string ppszVersionString);

    void GetIndexerVersion(out uint pdwMajor, out uint pdwMinor);

    IntPtr GetParameter(string pszName);

    void SetParameter(string pszName, ref object pValue);

    string ProxyName { get; }

    string BypassList { get; }

    void SetProxy(
      object sUseProxy,
      int fLocalByPassProxy,
      uint dwPortNumber,
      string pszProxyName,
      string pszByPassList);

    IntPtr GetCatalog(string pszCatalog);

    string UserAgent { get; set; }

    object UseProxy { get; }

    int LocalBypass { get; }

    uint PortNumber { get; }
}
