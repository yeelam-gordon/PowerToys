// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF50")]
public partial interface ISearchCatalogManager
{
    string Name
    {
        [return: MarshalAs(UnmanagedType.LPWStr)]
        get;
    }

    IntPtr GetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName);

    void SetParameter([MarshalAs(UnmanagedType.LPWStr)] string pszName, ref object pValue);

    void GetCatalogStatus(out object pStatus, out object pPausedReason);

    void Reset();

    void Reindex();

    void ReindexMatchingURLs([MarshalAs(UnmanagedType.LPWStr)] string pszPattern);

    void ReindexSearchRoot([MarshalAs(UnmanagedType.LPWStr)] string pszRoot);

    uint ConnectTimeout
    {
        set;
        get;
    }

    uint DataTimeout
    {
        set;
        get;
    }

    int NumberOfItems();

    void NumberOfItemsToIndex(
      out int plIncrementalCount,
      out int plNotificationQueue,
      out int plHighPriorityQueue);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string URLBeingIndexed();

    uint GetURLIndexingState([MarshalAs(UnmanagedType.LPWStr)] string psz);

    [return: MarshalAs(UnmanagedType.Interface)]
    object GetPersistentItemsChangedSink();

    void RegisterViewForNotification(
      [MarshalAs(UnmanagedType.LPWStr)] string pszView,
      [MarshalAs(UnmanagedType.Interface)] object pViewChangedSink,
      out uint pdwCookie);

    void GetItemsChangedSink(
      [MarshalAs(UnmanagedType.Interface)] object pISearchNotifyInlineSite,
      ref Guid riid,
      out IntPtr ppv,
      out Guid pGUIDCatalogResetSignature,
      out Guid pGUIDCheckPointSignature,
      out uint pdwLastCheckPointNumber);

    void UnregisterViewForNotification(uint dwCookie);

    void SetExtensionClusion([MarshalAs(UnmanagedType.LPWStr)] string pszExtension, int fExclude);

    [return: MarshalAs(UnmanagedType.Interface)]
    object EnumerateExcludedExtensions();

    [return: MarshalAs(UnmanagedType.Interface)]
    CSearchQueryHelper GetQueryHelper();

    int DiacriticSensitivity
    {
        set;
        get;
    }

    [return: MarshalAs(UnmanagedType.Interface)]
    object GetCrawlScopeManager();
}