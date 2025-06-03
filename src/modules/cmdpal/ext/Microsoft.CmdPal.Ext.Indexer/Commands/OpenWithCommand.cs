// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Native;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

internal sealed partial class OpenWithCommand : InvokableCommand
{
    private readonly IndexerItem _item;

    private static unsafe bool OpenWith(string filename)
    {
        var filenamePtr = Marshal.StringToHGlobalUni(filename);
        var verbPtr = Marshal.StringToHGlobalUni("openas");

        try
        {
            var info = new Win32Apis.ShellExecuteInfo
            {
                cbSize = (uint)Marshal.SizeOf<Win32Apis.ShellExecuteInfo>(),
                lpVerb = verbPtr,
                lpFile = filenamePtr,
                nShow = 1, // SW_SHOWNORMAL
                fMask = NativeHelpers.SEEMASKINVOKEIDLIST,
            };

            return Win32Apis.ShellExecuteExW(ref info);
        }
        finally
        {
            Marshal.FreeHGlobal(filenamePtr);
            Marshal.FreeHGlobal(verbPtr);
        }
    }

    internal OpenWithCommand(IndexerItem item)
    {
        this._item = item;
        this.Name = Resources.Indexer_Command_OpenWith;
        this.Icon = new IconInfo("\uE7AC");
    }

    public override CommandResult Invoke()
    {
        OpenWith(_item.FullPath);

        return CommandResult.GoHome();
    }
}
