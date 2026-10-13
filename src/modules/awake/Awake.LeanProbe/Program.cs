// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Awake.Core;

namespace Awake.LeanProbe;

internal static unsafe class Program
{
    private const string WindowClassName = "Awake.MessageWindow";
    private const uint WmTimer = 0x0113;
    private const uint WmClose = 0x0010;
    private const uint WmSessionChange = 0x02B1;
    private const uint WmSettingsChanged = 0x8001;
    private const uint WmTray = 0x8002;
    private const uint Continuous = 0x80000000;
    private const uint SystemRequired = 1;
    private const uint DisplayRequired = 2;

    private static nint _window;
    private static nint _icon;
    private static nint _boundProcess;
    private static bool _running = true;
    private static bool _locked;
    private static bool _display;
    private static bool _timerActive;
    private static bool _trayAdded;
    private static bool _sessionRegistered;
    private static int _mode;
    private static DateTimeOffset? _deadline;
    private static string? _settingsPath;
    private static Native.NotifyIcon _notification;
    private static FileSystemWatcher? _watcher;
    private static Exception? _messageError;

    private static int Main(string[] arguments)
    {
        nint instance = Native.GetModuleHandle(null);
        try
        {
            ParseArguments(arguments);
            if (_settingsPath != null)
            {
                ReadSettings();
            }

            fixed (char* className = WindowClassName)
            {
                Native.WindowClass windowClass = new()
                {
                    Size = (uint)sizeof(Native.WindowClass),
                    Instance = instance,
                    ClassName = className,
                    Procedure = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&WindowProcedure,
                };
                Check(Native.RegisterClassEx(in windowClass) != 0, "RegisterClassEx");
            }

            _window = Native.CreateWindowEx(0, WindowClassName, "Awake C# architecture probe", 0, 0, 0, 0, 0, 0, 0, instance, 0);
            Check(_window != 0, "CreateWindowEx");
            Check(Native.WTSRegisterSessionNotification(_window, 0) != 0, "WTSRegisterSessionNotification");
            _sessionRegistered = true;
            SetPowerRequest();
            AddTrayIcon();
            if (_deadline != null)
            {
                Check(Native.SetTimer(_window, 1, 1000, 0) != 0, "SetTimer");
                _timerActive = true;
            }

            if (_settingsPath != null)
            {
                _watcher = new FileSystemWatcher(Path.GetDirectoryName(_settingsPath)!, Path.GetFileName(_settingsPath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                };
                _watcher.Changed += SettingsChanged;
                _watcher.Created += SettingsChanged;
                _watcher.Renamed += SettingsChanged;
                _watcher.EnableRaisingEvents = true;
            }

            RunMessageLoop();
            if (_messageError != null)
            {
                throw new InvalidOperationException("A window message failed.", _messageError);
            }

            return 0;
        }
        catch (Exception exception) when (exception is Win32Exception or ArgumentException or IOException or JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            _watcher?.Dispose();
            ReportCleanup(Native.SetThreadExecutionState(Continuous) != 0, "release power request");
            if (_window != 0)
            {
                if (_timerActive)
                {
                    ReportCleanup(Native.KillTimer(_window, 1) != 0, "remove timer");
                }

                if (_trayAdded)
                {
                    ReportCleanup(Native.ShellNotifyIcon(2, in _notification) != 0, "remove tray icon");
                }

                if (_sessionRegistered)
                {
                    ReportCleanup(Native.WTSUnRegisterSessionNotification(_window) != 0, "unregister session notifications");
                }

                ReportCleanup(Native.DestroyWindow(_window) != 0, "destroy window");
            }

            if (_icon != 0)
            {
                ReportCleanup(Native.DestroyIcon(_icon) != 0, "destroy icon");
            }

            if (_boundProcess != 0)
            {
                ReportCleanup(Native.CloseHandle(_boundProcess) != 0, "close process handle");
            }

            Native.UnregisterClass(WindowClassName, instance);
        }
    }

    private static void ParseArguments(string[] arguments)
    {
        if (!CommandLineArgumentNormalizer.TryNormalize(arguments, DateTimeOffset.Now, CultureInfo.CurrentCulture, out string[] normalized, out var error, out var value))
        {
            throw new ArgumentException($"{error}: {value}");
        }

        _mode = 1;
        for (int index = 0; index < normalized.Length; index++)
        {
            switch (normalized[index])
            {
                case "--time-limit":
                    if (!uint.TryParse(NextValue(normalized, ref index), NumberStyles.None, CultureInfo.InvariantCulture, out uint seconds))
                    {
                        throw new ArgumentException("The probe duration must be an unsigned number of seconds.");
                    }

                    _deadline = DateTimeOffset.Now.AddSeconds(seconds);
                    _mode = 2;
                    break;
                case "--expire-at":
                    if (!DateTimeOffset.TryParse(NextValue(normalized, ref index), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset expiration))
                    {
                        throw new ArgumentException("The probe expiration must be a valid date and time.");
                    }

                    _deadline = expiration;
                    _mode = 3;
                    break;
                case "--display-on":
                    _display = true;
                    break;
                case "--pid":
                    if (!uint.TryParse(NextValue(normalized, ref index), NumberStyles.None, CultureInfo.InvariantCulture, out uint pid) || pid == 0)
                    {
                        throw new ArgumentException("The probe PID must be a positive number.");
                    }

                    _boundProcess = Native.OpenProcess(0x00100000, 0, pid);
                    Check(_boundProcess != 0, "OpenProcess");
                    break;
                case "--settings":
                    _settingsPath = Path.GetFullPath(NextValue(normalized, ref index));
                    break;
                default:
                    throw new ArgumentException($"Unsupported probe option: {normalized[index]}");
            }
        }
    }

    private static string NextValue(string[] arguments, ref int index)
    {
        string option = arguments[index];
        if (++index == arguments.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return arguments[index];
    }

    private static void ReadSettings()
    {
        using FileStream stream = new(_settingsPath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        ProbeSettings settings = JsonSerializer.Deserialize(stream, ProbeJsonContext.Default.ProbeSettings)
            ?? throw new JsonException("Settings cannot be null.");
        ProbeProperties properties = settings.Properties;
        _mode = properties.Mode;
        _display = properties.KeepDisplayOn;
        _deadline = _mode switch
        {
            0 or 1 => null,
            2 => DateTimeOffset.Now.AddSeconds(((ulong)properties.IntervalHours * 3600) + ((ulong)properties.IntervalMinutes * 60)),
            3 => properties.ExpirationDateTime,
            _ => throw new JsonException("Unsupported Awake mode."),
        };
    }

    private static void SetPowerRequest()
    {
        uint flags = Continuous;
        if (_mode != 0)
        {
            flags |= SystemRequired;
            if (_display && !_locked)
            {
                flags |= DisplayRequired;
            }
        }

        Check(Native.SetThreadExecutionState(flags) != 0, "SetThreadExecutionState");
    }

    private static void AddTrayIcon()
    {
        string fileName = _mode switch
        {
            0 => "disabled.ico",
            1 => "indefinite.ico",
            2 => "timed.ico",
            _ => "expirable.ico",
        };
        _icon = Native.LoadImage(0, Path.Combine(AppContext.BaseDirectory, "Assets", "Awake", fileName), 1, Native.GetSystemMetrics(49), Native.GetSystemMetrics(50), 0x10);
        Check(_icon != 0, "LoadImage");
        _notification = new Native.NotifyIcon
        {
            Size = (uint)sizeof(Native.NotifyIcon),
            Window = _window,
            Id = 1000,
            Flags = 1 | 2 | 4 | 0x80,
            Callback = WmTray,
            Icon = _icon,
        };
        UpdateTooltip();
        Check(Native.ShellNotifyIcon(0, in _notification) != 0, "Shell_NotifyIcon add");
        _trayAdded = true;
        _notification.Version = 4;
        Check(Native.ShellNotifyIcon(4, in _notification) != 0, "Shell_NotifyIcon version");
    }

    private static void UpdateTooltip()
    {
        string text = _deadline is { } deadline
            ? $"Awake C# probe\n{Math.Max(0, (int)Math.Ceiling((deadline - DateTimeOffset.Now).TotalSeconds))} seconds remaining"
            : $"Awake C# probe\nMode {_mode}";
        fixed (char* tip = _notification.Tip)
        {
            Span<char> destination = new(tip, 128);
            destination.Clear();
            text.AsSpan(0, Math.Min(text.Length, 127)).CopyTo(destination);
        }
    }

    private static void RunMessageLoop()
    {
        while (_running)
        {
            nint handle = _boundProcess;
            uint count = handle == 0 ? 0u : 1u;
            uint result = Native.MsgWaitForMultipleObjectsEx(count, &handle, uint.MaxValue, 0x04FF, 4);
            Check(result != uint.MaxValue, "MsgWaitForMultipleObjectsEx");
            if (count == 1 && result == 0)
            {
                _running = false;
            }

            while (Native.PeekMessage(out var message, 0, 0, 0, 1) != 0)
            {
                Native.TranslateMessage(in message);
                Native.DispatchMessage(in message);
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam)
    {
        try
        {
            switch (message)
            {
                case WmClose:
                    _running = false;
                    return 0;
                case WmTimer:
                    if (_deadline <= DateTimeOffset.Now)
                    {
                        _running = false;
                    }
                    else
                    {
                        UpdateTooltip();
                        _notification.Flags = 4;
                        Check(Native.ShellNotifyIcon(1, in _notification) != 0, "Shell_NotifyIcon tooltip");
                    }

                    return 0;
                case WmSessionChange:
                    if (wParam is 7 or 8)
                    {
                        _locked = wParam == 7;
                        SetPowerRequest();
                    }

                    return 0;
                case WmSettingsChanged:
                    ReadSettings();
                    SetPowerRequest();
                    if (_timerActive)
                    {
                        Check(Native.KillTimer(_window, 1) != 0, "KillTimer");
                        _timerActive = false;
                    }

                    if (_deadline != null)
                    {
                        Check(Native.SetTimer(_window, 1, 1000, 0) != 0, "SetTimer");
                        _timerActive = true;
                    }

                    return 0;
            }
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or JsonException or InvalidOperationException)
        {
            _messageError = exception;
            _running = false;
            return 0;
        }

        return Native.DefWindowProc(window, message, wParam, lParam);
    }

    private static void SettingsChanged(object sender, FileSystemEventArgs arguments)
    {
        if (Native.PostMessage(_window, WmSettingsChanged, 0, 0) == 0)
        {
            Console.Error.WriteLine("Failed to post a settings-change notification.");
        }
    }

    private static void Check(bool success, string operation)
    {
        if (!success)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), operation);
        }
    }

    private static void ReportCleanup(bool success, string operation)
    {
        if (!success)
        {
            Console.Error.WriteLine($"Cleanup failed: {operation}.");
        }
    }
}
