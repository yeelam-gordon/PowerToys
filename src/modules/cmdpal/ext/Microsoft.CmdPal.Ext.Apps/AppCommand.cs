// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32;
using Windows.Win32.System.Com;
using WyHash;

namespace Microsoft.CmdPal.Ext.Apps;

internal sealed partial class AppCommand : InvokableCommand
{
    private readonly AppItem _app;

    internal AppCommand(AppItem app)
    {
        _app = app;

        Name = Resources.run_command_action;
        Id = GenerateId();
    }

    internal static async Task StartApp(string aumid)
    {
        var clsid = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"); // ApplicationActivationManager CLSID
        var iid = typeof(IApplicationActivationManager).GUID;
        
        var hr = PInvoke.CoCreateInstance(clsid, null, CLSCTX.CLSCTX_LOCAL_SERVER, iid, out var appManagerObj);
        if (hr.Failed)
        {
            Logger.LogError($"Failed to create ApplicationActivationManager: {hr}");
            return;
        }
        
        var appManager = (IApplicationActivationManager)appManagerObj;
        const ActivateOptions noFlags = ActivateOptions.None;
        await Task.Run(() =>
        {
            try
            {
                appManager.ActivateApplication(aumid, /*queryArguments*/ string.Empty, noFlags, out var unusedPid);
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }).ConfigureAwait(false);
    }

    internal static async Task StartExe(string path)
    {
        await Task.Run(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        });
    }

    internal async Task Launch()
    {
        if (_app.IsPackaged)
        {
            await StartApp(_app.UserModelId);
        }
        else
        {
            await StartExe(_app.ExePath);
        }
    }

    public override CommandResult Invoke()
    {
        _ = Launch();
        return CommandResult.Dismiss();
    }

    private string GenerateId()
    {
        // Use WyHash64 to generate stable ID hashes.
        // manually seeding with 0, so that the hash is stable across launches
        var result = WyHash64.ComputeHash64(_app.Name + _app.Subtitle + _app.ExePath, seed: 0);
        return $"{_app.Name}_{result}";
    }
}
