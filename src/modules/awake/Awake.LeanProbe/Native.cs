// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Awake.LeanProbe;

internal static unsafe partial class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowClass
    {
        internal uint Size;
        internal uint Style;
        internal nint Procedure;
        internal int ClassExtra;
        internal int WindowExtra;
        internal nint Instance;
        internal nint Icon;
        internal nint Cursor;
        internal nint Background;
        internal char* MenuName;
        internal char* ClassName;
        internal nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Window;
        internal uint Id;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal int X;
        internal int Y;
        internal uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NotifyIcon
    {
        internal uint Size;
        internal nint Window;
        internal uint Id;
        internal uint Flags;
        internal uint Callback;
        internal nint Icon;
        internal fixed char Tip[128];
        internal uint State;
        internal uint StateMask;
        internal fixed char Info[256];
        internal uint Version;
        internal fixed char InfoTitle[64];
        internal uint InfoFlags;
        internal Guid IconGuid;
        internal nint BalloonIcon;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? name);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static partial ushort RegisterClassEx(in WindowClass value);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint CreateWindowEx(uint extendedStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    internal static partial int PeekMessage(out Message message, nint window, uint first, uint last, uint remove);

    [LibraryImport("user32.dll")]
    internal static partial int TranslateMessage(in Message message);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessage(in Message message);

    [LibraryImport("user32.dll")]
    internal static partial uint MsgWaitForMultipleObjectsEx(uint count, nint* handles, uint timeout, uint wakeMask, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nuint SetTimer(nint window, nuint id, uint milliseconds, nint procedure);

    [LibraryImport("user32.dll")]
    internal static partial int KillTimer(nint window, nuint id);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    internal static partial int PostMessage(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    internal static partial int DestroyWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int UnregisterClass(string className, nint instance);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint OpenProcess(uint access, int inherit, uint pid);

    [LibraryImport("kernel32.dll")]
    internal static partial int CloseHandle(nint handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint SetThreadExecutionState(uint flags);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint LoadImage(nint instance, string name, uint type, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    internal static partial int DestroyIcon(nint icon);

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    internal static partial int ShellNotifyIcon(uint message, in NotifyIcon data);

    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    internal static partial int WTSRegisterSessionNotification(nint window, uint flags);

    [LibraryImport("wtsapi32.dll")]
    internal static partial int WTSUnRegisterSessionNotification(nint window);
}
