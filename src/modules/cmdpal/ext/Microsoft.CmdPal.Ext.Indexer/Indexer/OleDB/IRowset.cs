// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

[GeneratedComInterface]
[Guid("0c733a7c-2a1c-11ce-ade5-00aa0044773d")]
public partial interface IRowset
{
    [PreserveSig]
    int AddRefRows(
        uint cRows,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] nint[] rghRows,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] rgRefCounts,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] rgRowStatus);

    [PreserveSig]
    int GetData(
        nint hRow,
        nint hAccessor,
        nint pData);

    [PreserveSig]
    int GetNextRows(
       nint hReserved,
       long lRowsOffset,
       long cRows,
       out uint pcRowsObtained,
       out nint prghRows);

    [PreserveSig]
    int ReleaseRows(
        uint cRows,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] nint[] rghRows,
        nint rgRowOptions,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] rgRefCounts,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] rgRowStatus);
}
