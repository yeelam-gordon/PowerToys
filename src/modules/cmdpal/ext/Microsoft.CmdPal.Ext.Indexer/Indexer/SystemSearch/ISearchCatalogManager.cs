// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF50")]
public partial interface ISearchCatalogManager
{
    string Name { get; }

    IntPtr GetParameter(string pszName);

    void SetParameter(string pszName, ref object pValue);

    void GetCatalogStatus(out object pStatus, out object pPausedReason);

    void Reset();

    void Reindex();

    void ReindexMatchingURLs(string pszPattern);

    void ReindexSearchRoot(string pszRoot);

    uint ConnectTimeout { get; set; }

    uint DataTimeout { get; set; }

    int NumberOfItems();

    void NumberOfItemsToIndex(
      out int plIncrementalCount,
      out int plNotificationQueue,
      out int plHighPriorityQueue);

    string URLBeingIndexed();

    uint GetURLIndexingState(string psz);

    IntPtr GetPersistentItemsChangedSink();

    void RegisterViewForNotification(
      string pszView,
      IntPtr pViewChangedSink,
      out uint pdwCookie);

    void GetItemsChangedSink(
      IntPtr pISearchNotifyInlineSite,
      in Guid riid,
      out IntPtr ppv,
      out Guid pGUIDCatalogResetSignature,
      out Guid pGUIDCheckPointSignature,
      out uint pdwLastCheckPointNumber);

    void UnregisterViewForNotification(uint dwCookie);

    void SetExtensionClusion(string pszExtension, int fExclude);

    IntPtr EnumerateExcludedExtensions();

    ISearchQueryHelper GetQueryHelper();

    int DiacriticSensitivity { get; set; }

    IntPtr GetCrawlScopeManager();
}
