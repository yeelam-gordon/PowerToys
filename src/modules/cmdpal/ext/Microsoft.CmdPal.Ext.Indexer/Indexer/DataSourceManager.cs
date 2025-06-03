// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Native;
using Windows.Win32.System.Search;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer;

internal static class DataSourceManager
{
    private static readonly Guid CLSIDCollatorDataSource = new("9E175B8B-F52A-11D8-B9A5-505054503030");

    private static IDBInitialize _dataSource;

    public static IDBInitialize GetDataSource()
    {
        if (_dataSource == null)
        {
            InitializeDataSource();
        }

        return _dataSource;
    }

    private static bool InitializeDataSource()
    {
        var clsid = CLSIDCollatorDataSource;
        var iid = typeof(IDBInitialize).GUID;
        var hr = Win32Apis.CoCreateInstance(ref clsid, 0, Win32Apis.CLSCTX_INPROC_SERVER, ref iid, out var dataSourcePtr);
        if (hr != 0)
        {
            Logger.LogError("CoCreateInstance failed: " + hr);
            return false;
        }

        if (dataSourcePtr == 0)
        {
            Logger.LogError("CoCreateInstance failed: dataSourcePtr is null");
            return false;
        }

        // Use ComWrappers to create the managed wrapper
        var comWrappers = new IndexerComWrappers();
        var dataSourceObj = comWrappers.GetOrCreateObjectForComInstance(dataSourcePtr, CreateObjectFlags.None);
        _dataSource = (IDBInitialize)dataSourceObj;
        _dataSource.Initialize();

        return true;
    }
}
