// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;
using Microsoft.CmdPal.Ext.Indexer.Interop;
using System;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;

internal sealed class QueryStringBuilder
{
    private const string Properties = "System.ItemUrl, System.ItemNameDisplay, path, System.Search.EntryID, System.Kind, System.KindText";
    private const string SystemIndex = "SystemIndex";
    private const string ScopeFileConditions = "SCOPE='file:'";
    private const string OrderConditions = "System.DateModified DESC";
    private const string SelectQueryWithScope = "SELECT " + Properties + " FROM " + SystemIndex + " WHERE (" + ScopeFileConditions + ")";
    private const string SelectQueryWithScopeAndOrderConditions = SelectQueryWithScope + " ORDER BY " + OrderConditions;

    private static readonly Guid CLSIDSearchManager = new("7D096C5F-AC08-4f1f-BEB7-5C22C517CE39");
    private static readonly StrategyBasedComWrappers s_comWrappers = new();
    private static ISearchQueryHelper queryHelper;

    public static string GeneratePrimingQuery() => SelectQueryWithScopeAndOrderConditions;

    public static string GenerateQuery(string searchText, uint whereId)
    {
        if (queryHelper == null)
        {
            // Create SearchManager using CoCreateInstance
            var hr = ComApi.CoCreateInstance(
                CLSIDSearchManager,
                IntPtr.Zero,
                CLSCTX.CLSCTX_INPROC_SERVER,
                typeof(ISearchManager).GUID,
                out var searchManagerPtr);

            if (hr.Failed || searchManagerPtr == IntPtr.Zero)
            {
                throw new Exception($"Failed to create SearchManager: {hr}");
            }

            try
            {
                var searchManagerObj = s_comWrappers.GetOrCreateObjectForComInstance(searchManagerPtr, CreateObjectFlags.None);
                var searchManager = (ISearchManager)searchManagerObj;
                
                var catalogManager = searchManager.GetCatalog(SystemIndex);
                if (catalogManager == null)
                {
                    throw new Exception("Failed to get catalog manager");
                }

                queryHelper = catalogManager.GetQueryHelper();
                if (queryHelper == null)
                {
                    throw new Exception("Failed to get query helper");
                }

                queryHelper.QuerySelectColumns = Properties;
                queryHelper.QueryContentProperties = "System.FileName";
                queryHelper.QuerySorting = OrderConditions;
                
                // No need to release GeneratedComInterface objects - ComWrappers handles their lifecycle
            }
            finally
            {
                Marshal.Release(searchManagerPtr);
            }
        }

        queryHelper.QueryWhereRestrictions = "AND " + ScopeFileConditions + "AND ReuseWhere(" + whereId.ToString(CultureInfo.InvariantCulture) + ")";
        return queryHelper.GenerateSQLFromUserQuery(searchText);
    }
}
