// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

// Search Catalog Manager CLSID - removed ComImport class and replaced with CLSID constant
internal static class SearchCatalogManagerCLSID
{
    public static readonly Guid CLSID_CSearchCatalogManager = new("AAB49DD5-AD0B-40AE-B654-AE8976BF6BD2");
}
