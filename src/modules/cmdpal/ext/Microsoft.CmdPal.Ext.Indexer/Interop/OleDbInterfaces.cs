// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Microsoft.CmdPal.Ext.Indexer.Interop;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0c733a8b-2a1c-11ce-ade5-00aa0044773d")]
public partial interface IDBInitialize
{
    void Initialize();
    void Uninitialize();
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0c733a9c-2a1c-11ce-ade5-00aa0044773d")]
public partial interface IDBCreateSession
{
    void CreateSession(IntPtr pUnkOuter, in Guid riid, out IDBCreateCommand? ppDBSession);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0c733a7d-2a1c-11ce-ade5-00aa0044773d")]
public partial interface IDBCreateCommand
{
    void CreateCommand(IntPtr pUnkOuter, in Guid riid, out ICommandText? ppCommand);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0c733a27-2a1c-11ce-ade5-00aa0044773d")]
public partial interface ICommandText
{
    void GetCommandText(out Guid pguidDialect, out string ppwszCommand);
    void SetCommandText(in Guid rguidDialect, string pwszCommand);
    void GetParameterInfo(out uint pcParams, out IntPtr prgParamInfo, out IntPtr ppNamesBuffer);
    void MapParameterNames(uint cParamNames, string[] rgParamNames, out IntPtr rgParamOrdinals);
    void SetParameterInfo(uint cParams, uint[] rgParamOrdinals, IntPtr rgParamBindInfo);
    void Prepare(uint cExpectedRuns);
    void Unprepare();
    void Cancel();
    void Execute(IntPtr pUnkOuter, in Guid riid, IntPtr pParams, out int pcRowsAffected, out IRowset? ppRowset);
    void GetDBSession(in Guid riid, out IDBCreateSession? ppSession);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0c733a83-2a1c-11ce-ade5-00aa0044773d")]
public partial interface IGetRow
{
    void GetRowFromHROW(IntPtr pUnkOuter, nuint hRow, in Guid riid, out IPropertyStore? ppUnk);
    void GetURLFromHROW(nuint hRow, out string ppwszURL);
}