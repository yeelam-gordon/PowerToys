// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.CmdPal.Ext.Indexer.Interop;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0C733A55-2A1C-11CE-ADE5-00AA0044773D")]
public partial interface IRowsetInfo
{
    int GetProperties(
        uint cPropertyIDSets,
        DBPROPIDSET[] rgPropertyIDSets,
        out ulong pcPropertySets,
        out IntPtr prgPropertySets);

    int GetReferencedRowset(
        uint iOrdinal,
        in Guid riid,
        out IRowset? ppReferencedRowset);

    int GetSpecification(
        in Guid riid,
        out ICommandText? ppSpecification);
}
