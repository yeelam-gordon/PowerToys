// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0c733a7c-2a1c-11ce-ade5-00aa0044773d")]
public partial interface IRowset
{
    int AddRefRows(
        uint cRows,
        IntPtr[] rghRows,
        uint[] rgRefCounts,
        int[] rgRowStatus);

    int GetData(
        IntPtr hRow,
        IntPtr hAccessor,
        IntPtr pData);

    int GetNextRows(
       IntPtr hReserved,
       long lRowsOffset,
       long cRows,
       out uint pcRowsObtained,
       out IntPtr prghRows);

    int ReleaseRows(
        uint cRows,
        IntPtr[] rghRows,
        IntPtr rgRowOptions,
        uint[] rgRefCounts,
        int[] rgRowStatus);
}
