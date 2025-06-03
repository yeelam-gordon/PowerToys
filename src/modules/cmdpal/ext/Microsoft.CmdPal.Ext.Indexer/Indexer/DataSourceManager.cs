// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Interop;
using System.Runtime.InteropServices;

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
        var hr = ComApi.CoCreateInstance(
            CLSIDCollatorDataSource, 
            IntPtr.Zero, 
            ClsContext.CLSCTX_INPROC_SERVER, 
            typeof(IDBInitialize).GUID, 
            out var dataSourcePtr);
            
        if (hr != 0)
        {
            Logger.LogError("CoCreateInstance failed: " + hr);
            return false;
        }

        if (dataSourcePtr == IntPtr.Zero)
        {
            Logger.LogError("CoCreateInstance failed: dataSourcePtr is null");
            return false;
        }

        try
        {
            var dataSourceObj = Marshal.GetObjectForIUnknown(dataSourcePtr);
            _dataSource = (IDBInitialize)dataSourceObj;
            _dataSource.Initialize();

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to cast COM object to IDBInitialize", ex);
            return false;
        }
        finally
        {
            if (dataSourcePtr != IntPtr.Zero)
            {
                Marshal.Release(dataSourcePtr);
            }
        }
    }
}
