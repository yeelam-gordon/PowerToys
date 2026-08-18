// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CmdPal.Common;
using Timer = System.Timers.Timer;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal sealed partial class PowerModeDataManager : IDisposable
{
    private const int OneSecondInMilliseconds = 1000;

    private readonly Timer _updateTimer;
    private readonly Action _updateAction;
    private readonly PowerModeService _powerModeService;
    private readonly EventHandler _powerModeChangedHandler;
    private readonly Lock _activateLock = new();
    private int _activateCount;
    private bool _updateFailureLogged;

    internal PowerModeDataManager(
        PowerModeService powerModeService,
        Action updateAction)
    {
        _powerModeService = powerModeService;
        _updateAction = updateAction;
        _powerModeChangedHandler = (_, _) => RunUpdateActionSafely();
        _updateTimer = new Timer(OneSecondInMilliseconds)
        {
            AutoReset = true,
            Enabled = false,
        };
        _updateTimer.Elapsed += (_, _) => RunUpdateActionSafely();
        _powerModeService.PowerModeChanged += _powerModeChangedHandler;
    }

    internal void PushActivate()
    {
        lock (_activateLock)
        {
            if (_activateCount++ == 0)
            {
                StartPolling();
            }
        }
    }

    internal void PopActivate()
    {
        lock (_activateLock)
        {
            _activateCount = Math.Max(0, _activateCount - 1);
            if (_activateCount == 0)
            {
                StopPolling();
            }
        }
    }

    public void Dispose()
    {
        StopPolling();
        _updateTimer.Dispose();
        _powerModeService.PowerModeChanged -= _powerModeChangedHandler;
    }

    private void StartPolling()
    {
        _powerModeService.EnsureSubscribed();
        if (RunUpdateActionSafely())
        {
            _updateTimer.Enabled = true;
        }
    }

    private void StopPolling()
    {
        _updateTimer.Enabled = false;
        _powerModeService.Unsubscribe();
    }

    private bool RunUpdateActionSafely()
    {
        try
        {
            _updateAction();
            return true;
        }
        catch (Exception ex)
        {
            StopPolling();
            if (!_updateFailureLogged)
            {
                _updateFailureLogged = true;
                CoreLogger.LogError("Unexpected exception while updating power mode data. Polling stopped.", ex);
            }

            return false;
        }
    }
}
