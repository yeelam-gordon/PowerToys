"""
keydriver.py - Real hardware-style key input via Win32 SendInput (ctypes).

PowerAccent installs a WH_KEYBOARD_LL global hook that does NOT filter injected
input (no LLKHF_INJECTED check in KeyboardListener::LowLevelKeyboardProc), so
SendInput-generated key events drive the real hook exactly like a physical
keyboard. winappcli cannot synthesize the held-letter + activation-key sequence
the hook needs, so we do it here.

Primitives: key_down / key_up (by virtual-key code) and higher-level trigger
sequences used by the sign-off harness.
"""
from __future__ import annotations

import ctypes
import time
from ctypes import wintypes

user32 = ctypes.WinDLL("user32", use_last_error=True)

# Virtual-key codes
VK = {
    "SPACE": 0x20, "LEFT": 0x25, "RIGHT": 0x27, "BACK": 0x08,
    "LSHIFT": 0xA0, "RSHIFT": 0xA1, "SHIFT": 0x10,
    "CAPITAL": 0x14, "CONTROL": 0x11,
}
for _c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ":
    VK[_c] = 0x41 + (ord(_c) - ord("A"))

KEYEVENTF_KEYUP = 0x0002
KEYEVENTF_EXTENDEDKEY = 0x0001
INPUT_KEYBOARD = 1

ULONG_PTR = ctypes.c_size_t  # pointer-sized (correct on x64)


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [
        ("wVk", wintypes.WORD),
        ("wScan", wintypes.WORD),
        ("dwFlags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("dwExtraInfo", ULONG_PTR),
    ]


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [
        ("dx", wintypes.LONG), ("dy", wintypes.LONG),
        ("mouseData", wintypes.DWORD), ("dwFlags", wintypes.DWORD),
        ("time", wintypes.DWORD), ("dwExtraInfo", ULONG_PTR),
    ]


class _INPUTunion(ctypes.Union):
    _fields_ = [("mi", MOUSEINPUT), ("ki", KEYBDINPUT)]


class INPUT(ctypes.Structure):
    _fields_ = [("type", wintypes.DWORD), ("u", _INPUTunion)]


user32.SendInput.argtypes = (wintypes.UINT, ctypes.POINTER(INPUT), ctypes.c_int)
user32.SendInput.restype = wintypes.UINT

_EXTENDED = {VK["LEFT"], VK["RIGHT"]}


def _send(vk: int, up: bool) -> None:
    flags = KEYEVENTF_KEYUP if up else 0
    if vk in _EXTENDED:
        flags |= KEYEVENTF_EXTENDEDKEY
    ki = KEYBDINPUT(wVk=vk, wScan=0, dwFlags=flags, time=0, dwExtraInfo=0)
    inp = INPUT(type=INPUT_KEYBOARD, u=_INPUTunion(ki=ki))
    n = user32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))
    if n != 1:
        raise ctypes.WinError(ctypes.get_last_error())


def key_down(name_or_vk) -> None:
    _send(_vk(name_or_vk), up=False)


def key_up(name_or_vk) -> None:
    _send(_vk(name_or_vk), up=True)


def tap(name_or_vk, hold: float = 0.03) -> None:
    key_down(name_or_vk)
    time.sleep(hold)
    key_up(name_or_vk)


def _vk(name_or_vk) -> int:
    if isinstance(name_or_vk, int):
        return name_or_vk
    return VK[name_or_vk.upper()]


def clear_modifiers() -> None:
    """Best-effort release of modifiers that a prior sequence may have left down."""
    for k in ("SHIFT", "LSHIFT", "RSHIFT", "CONTROL"):
        key_up(k)


# -----------------------------------------------------------------------------
# High-level PowerAccent trigger sequences.
#
# Timing model (default Both mode, inputTime=300ms):
#   - letter key-down starts the hook stopwatch; the base letter also types.
#   - the FIRST trigger key-down (space/arrow) shows the toolbar AND selects the
#     initial candidate (ProcessNextChar with _selectedIndex==-1).
#   - each further trigger key-down navigates.
#   - the letter key-up commits: if elapsed >= inputTime -> insert selected glyph
#     (Insert(..., back=true) backspaces the base letter first). If elapsed <
#     inputTime it is treated as a false start (no insert).
# -----------------------------------------------------------------------------

def summon_hold(letter: str, trigger: str = "SPACE", pre_hold: float = 0.45,
                extra_triggers: list[str] | None = None, nav_pause: float = 0.20):
    """
    Hold `letter`, press `trigger` (+ optional extra navigation triggers), and
    LEAVE the letter held down so the overlay stays visible for UIA reads.
    Returns a callable that releases the letter (commit).
    """
    key_down(letter)
    time.sleep(pre_hold)
    tap(trigger)                       # shows toolbar + selects initial candidate
    time.sleep(nav_pause)
    for t in (extra_triggers or []):
        tap(t)
        time.sleep(nav_pause)

    def release_commit():
        key_up(letter)
        time.sleep(0.15)
    return release_commit


def type_accent(letter: str, trigger: str = "SPACE", pre_hold: float = 0.45,
                extra_triggers: list[str] | None = None, nav_pause: float = 0.18,
                show_wait: float = 0.40):
    """Full commit path: summon, wait for the deferred render, then release."""
    release = summon_hold(letter, trigger, pre_hold, extra_triggers, nav_pause)
    time.sleep(show_wait)
    release()
