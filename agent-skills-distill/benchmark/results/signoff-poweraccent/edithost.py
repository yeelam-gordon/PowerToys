"""
edithost.py - A real Win32 EDIT control in a top-level window, used as the
deterministic text target for the PowerAccent sign-off.

The "EDIT" window class is the classic Win32 text box used by countless real
apps; it receives injected Unicode input exactly like any focused text field and
exposes the UIA ValuePattern, so winappcli can read its contents. This removes
the modern-Notepad RichEditBox UIA ambiguity from the sign-off.
"""
from __future__ import annotations

import ctypes
import threading
import time
from ctypes import wintypes

user32 = ctypes.WinDLL("user32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

WNDPROC = ctypes.WINFUNCTYPE(ctypes.c_ssize_t, wintypes.HWND, wintypes.UINT,
                             wintypes.WPARAM, wintypes.LPARAM)

user32.DefWindowProcW.argtypes = (wintypes.HWND, wintypes.UINT,
                                  wintypes.WPARAM, wintypes.LPARAM)
user32.DefWindowProcW.restype = ctypes.c_ssize_t
user32.CreateWindowExW.restype = wintypes.HWND
user32.SendMessageW.restype = ctypes.c_ssize_t

WS_OVERLAPPEDWINDOW = 0x00CF0000
WS_VISIBLE = 0x10000000
WS_CHILD = 0x40000000
WS_VSCROLL = 0x00200000
ES_MULTILINE = 0x0004
ES_AUTOVSCROLL = 0x0040
ES_WANTRETURN = 0x1000
WM_DESTROY = 0x0002
WM_SETTEXT = 0x000C
WM_GETTEXT = 0x000D
WM_GETTEXTLENGTH = 0x000E
WM_SIZE = 0x0005
SW_SHOW = 5
SW_RESTORE = 9


class WNDCLASS(ctypes.Structure):
    _fields_ = [
        ("style", wintypes.UINT),
        ("lpfnWndProc", WNDPROC),
        ("cbClsExtra", ctypes.c_int),
        ("cbWndExtra", ctypes.c_int),
        ("hInstance", wintypes.HINSTANCE),
        ("hIcon", wintypes.HICON),
        ("hCursor", wintypes.HANDLE),
        ("hbrBackground", wintypes.HBRUSH),
        ("lpszMenuName", wintypes.LPCWSTR),
        ("lpszClassName", wintypes.LPCWSTR),
    ]


class EditHost:
    def __init__(self, title: str = "AccentTargetBox"):
        self.title = title
        self.hwnd = None
        self.edit = None
        self._ready = threading.Event()
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()
        if not self._ready.wait(5):
            raise RuntimeError("EditHost window did not initialize")

    def _run(self):
        hInst = kernel32.GetModuleHandleW(None)
        clsname = "AccentEditHostCls"

        def wndproc(hwnd, msg, wparam, lparam):
            if msg == WM_SIZE and self.edit:
                w = lparam & 0xFFFF
                h = (lparam >> 16) & 0xFFFF
                user32.MoveWindow(self.edit, 0, 0, w, h, True)
                return 0
            if msg == WM_DESTROY:
                user32.PostQuitMessage(0)
                return 0
            return user32.DefWindowProcW(hwnd, msg, wparam, lparam)

        self._wp = WNDPROC(wndproc)
        wc = WNDCLASS()
        wc.lpfnWndProc = self._wp
        wc.hInstance = hInst
        wc.lpszClassName = clsname
        wc.hCursor = user32.LoadCursorW(None, 32512)
        wc.hbrBackground = 6  # COLOR_WINDOW+1
        user32.RegisterClassW(ctypes.byref(wc))

        self.hwnd = user32.CreateWindowExW(
            0, clsname, self.title, WS_OVERLAPPEDWINDOW | WS_VISIBLE,
            200, 200, 700, 400, None, None, hInst, None)
        self.edit = user32.CreateWindowExW(
            0, "EDIT", None,
            WS_CHILD | WS_VISIBLE | WS_VSCROLL | ES_MULTILINE | ES_AUTOVSCROLL | ES_WANTRETURN,
            0, 0, 700, 400, self.hwnd, None, hInst, None)
        user32.ShowWindow(self.hwnd, SW_SHOW)
        user32.UpdateWindow(self.hwnd)
        self._ready.set()

        msg = wintypes.MSG()
        while user32.GetMessageW(ctypes.byref(msg), None, 0, 0) > 0:
            user32.TranslateMessage(ctypes.byref(msg))
            user32.DispatchMessageW(ctypes.byref(msg))

    def focus(self):
        # Allow a non-elevated window to take foreground: clear the foreground lock
        # timeout, then minimize/restore + SetForegroundWindow to force our EDIT forward.
        SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001
        SPIF_SENDCHANGE = 0x02
        user32.SystemParametersInfoW(SPI_SETFOREGROUNDLOCKTIMEOUT, 0,
                                     ctypes.cast(0, ctypes.c_void_p), SPIF_SENDCHANGE)
        cur = kernel32.GetCurrentThreadId()
        HWND_TOPMOST = ctypes.c_void_p(-1)
        HWND_NOTOPMOST = ctypes.c_void_p(-2)
        SWP_NOMOVE = 0x0002; SWP_NOSIZE = 0x0001; SWP_SHOWWINDOW = 0x0040
        user32.ShowWindow(self.hwnd, 6)   # SW_MINIMIZE
        time.sleep(0.05)
        user32.ShowWindow(self.hwnd, SW_RESTORE)
        user32.SetWindowPos(self.hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW)
        user32.BringWindowToTop(self.hwnd)
        try:
            user32.SwitchToThisWindow(self.hwnd, True)
        except Exception:
            pass
        user32.SetForegroundWindow(self.hwnd)
        t2 = user32.GetWindowThreadProcessId(self.hwnd, None)
        user32.AttachThreadInput(cur, t2, True)
        user32.SetForegroundWindow(self.hwnd)
        user32.SetActiveWindow(self.hwnd)
        user32.SetFocus(self.edit)
        user32.AttachThreadInput(cur, t2, False)
        user32.SetWindowPos(self.hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE)
        time.sleep(0.25)
        return user32.GetForegroundWindow() == self.hwnd

    def clear(self):
        user32.SendMessageW(self.edit, WM_SETTEXT, 0, ctypes.c_wchar_p(""))

    def get_text(self) -> str:
        n = user32.SendMessageW(self.edit, WM_GETTEXTLENGTH, 0, 0)
        buf = ctypes.create_unicode_buffer(n + 1)
        user32.SendMessageW(self.edit, WM_GETTEXT, n + 1, buf)
        return buf.value

    def close(self):
        if self.hwnd:
            user32.PostMessageW(self.hwnd, WM_DESTROY, 0, 0)


if __name__ == "__main__":
    h = EditHost()
    print("hwnd", h.hwnd, "edit", h.edit)
    h.focus()
    h.clear()
    time.sleep(0.3)
    print("text:", repr(h.get_text()))
    time.sleep(2)
