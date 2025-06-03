// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

// Search Query Helper CLSID - removed ComImport class and replaced with CLSID constant
internal static class SearchQueryHelperCLSID
{
    public static readonly Guid CLSID_CSearchQueryHelper = new("B271E955-09E1-42E1-9B95-5994A534B613");
}
