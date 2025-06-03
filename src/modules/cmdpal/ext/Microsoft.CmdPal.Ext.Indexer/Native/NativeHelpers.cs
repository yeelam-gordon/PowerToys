// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Microsoft.CmdPal.Ext.Indexer.Native;

internal sealed class NativeHelpers
{
    public const uint SEEMASKINVOKEIDLIST = 12;

    internal static class PropertyKeys
    {
        public static readonly PROPERTYKEY PKEYItemNameDisplay = new() { fmtid = new System.Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), pid = 10 };
        public static readonly PROPERTYKEY PKEYItemUrl = new() { fmtid = new System.Guid("49691C90-7E17-101A-A91C-08002B2ECDA9"), pid = 9 };
        public static readonly PROPERTYKEY PKEYKindText = new() { fmtid = new System.Guid("F04BEF95-C585-4197-A2B7-DF46FDC9EE6D"), pid = 100 };
    }

    internal static class OleDb
    {
        public static readonly Guid DbGuidDefault = new("C8B521FB-5CF3-11CE-ADE5-00AA0044773D");
    }

    // Search Manager CLSIDs for COM object creation
    internal static class SearchCLSIDs
    {
        public static readonly Guid CLSID_CSearchManager = new("7D096C5F-AC08-4F1F-BEB7-5C22C517CE39");
        public static readonly Guid CLSID_CSearchCatalogManager = new("AAB49DD5-AD0B-40AE-B654-AE8976BF6BD2");
        public static readonly Guid CLSID_CSearchQueryHelper = new("B271E955-09E1-42E1-9B95-5994A534B613");
    }
}
