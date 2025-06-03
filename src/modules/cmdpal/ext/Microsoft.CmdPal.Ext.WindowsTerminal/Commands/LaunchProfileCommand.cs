// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI;
using Windows.Win32;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Commands;

internal sealed partial class LaunchProfileCommand : InvokableCommand
{
    private readonly string _id;
    private readonly string _profile;
    private readonly bool _openNewTab;
    private readonly bool _openQuake;

    internal LaunchProfileCommand(string id, string profile, string iconPath, bool openNewTab, bool openQuake)
    {
        this._id = id;
        this._profile = profile;
        this._openNewTab = openNewTab;
        this._openQuake = openQuake;

        this.Name = Resources.launch_profile;
        this.Icon = new IconInfo(iconPath);
    }

    private void Launch(string id, string profile)
    {
        try
        {
            ComWrappers cw = new StrategyBasedComWrappers();
            
            // Create the ApplicationActivationManager using ComWrappers
            var clsid = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"); // CLSID_ApplicationActivationManager
            var iid = typeof(IApplicationActivationManager).GUID;
            
            int hr = Windows.Win32.PInvoke.CoCreateInstance(in clsid, null, Windows.Win32.System.Com.CLSCTX.CLSCTX_INPROC_SERVER, in iid, out object obj);
            if (hr >= 0)
            {
                var appManager = (IApplicationActivationManager)cw.GetOrCreateObjectForComInstance((nint)obj, CreateObjectFlags.None);
                const Windows.Win32.UI.Shell.ACTIVATEOPTIONS noFlags = Windows.Win32.UI.Shell.ACTIVATEOPTIONS.AO_NONE;
                var queryArguments = TerminalHelper.GetArguments(profile, _openNewTab, _openQuake);
                
                hr = appManager.ActivateApplication(id, queryArguments, noFlags, out var unusedPid);
                if (hr < 0)
                {
                    Logger.LogError($"Failed to activate application: HRESULT = 0x{hr:X8}");
                }
            }
            else
            {
                Logger.LogError($"Failed to create ApplicationActivationManager: HRESULT = 0x{hr:X8}");
            }
        }
        catch (Exception ex)
        {
            // TODO GH #108 We need to figure out some logging
            // var name = "Plugin: " + Resources.plugin_name;
            // var message = Resources.run_terminal_failed;
            // Log.Exception("Failed to open Windows Terminal", ex, GetType());
            // _context.API.ShowMsg(name, message, string.Empty);
            Logger.LogError($"Failed to open Windows Terminal: {ex.Message}");
        }
    }
#pragma warning restore IDE0059, CS0168

    public override CommandResult Invoke()
    {
        try
        {
            Launch(_id, _profile);
        }
        catch
        {
            // TODO GH #108 We need to figure out some logging
            // No need to log here, as the exception is already logged in the Launch method
        }

        return CommandResult.Dismiss();
    }
}
