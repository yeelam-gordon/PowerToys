// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[GeneratedComInterface]
[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1212:Property accessors should follow order", Justification = "The order of the property accessors must match the order in which the methods were defined in the vtable")]
public partial interface ISearchQueryHelper
{
    string ConnectionString
    {
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    uint QueryContentLocale
    {
        set;
        get;
    }

    uint QueryKeywordLocale
    {
        set;
        get;
    }

    object QueryTermExpansion
    {
        set;
        get;
    }

    object QuerySyntax
    {
        set;
        get;
    }

    string QueryContentProperties
    {
        [param: MarshalAs(UnmanagedType.LPWStr)]
        set;
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    string QuerySelectColumns
    {
        [param: MarshalAs(UnmanagedType.LPWStr)]
        set;
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    string QueryWhereRestrictions
    {
        [param: MarshalAs(UnmanagedType.LPWStr)]
        set;
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    string QuerySorting
    {
        [param: MarshalAs(UnmanagedType.LPWStr)]
        set;
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GenerateSQLFromUserQuery([MarshalAs(UnmanagedType.LPWStr)] string pszQuery);

    void WriteProperties(
      int itemID,
      uint dwNumberOfColumns,
      ref object pColumns,
      ref object pValues,
      ref object pftGatherModifiedTime);

    int QueryMaxResults
    {
        set;
        get;
    }
}
