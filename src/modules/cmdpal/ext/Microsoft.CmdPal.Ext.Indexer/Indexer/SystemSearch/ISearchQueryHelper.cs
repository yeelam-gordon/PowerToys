// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63")]
public partial interface ISearchQueryHelper
{
    string ConnectionString { get; }

    uint QueryContentLocale { get; set; }

    uint QueryKeywordLocale { get; set; }

    object QueryTermExpansion { get; set; }

    object QuerySyntax { get; set; }

    string QueryContentProperties { get; set; }

    string QuerySelectColumns { get; set; }

    string QueryWhereRestrictions { get; set; }

    string QuerySorting { get; set; }

    string GenerateSQLFromUserQuery(string pszQuery);

    void WriteProperties(
      int itemID,
      uint dwNumberOfColumns,
      ref object pColumns,
      ref object pValues,
      ref object pftGatherModifiedTime);

    int QueryMaxResults { get; set; }
}
