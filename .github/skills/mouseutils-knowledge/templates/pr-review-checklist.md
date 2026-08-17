# Mouse Utilities — PR Review Checklist

Apply after reading the diff cold (see anti-anchoring in SKILL.md). Only check rows for the
sub-utility and code paths the PR actually touches — the four utilities are independent.

## Scope & correctness
- [ ] Change is confined to one sub-utility (`FindMyMouse` / `MouseHighlighter` / `MouseJump*` /
      `MousePointerCrosshairs`); no accidental cross-utility coupling.
- [ ] New settings have a matching default in the C++ `*Settings` struct **and** the C# Settings UI
      (`src/settings-ui/.../ViewModels`, `Settings.UI.Library`), including opacity/alpha encoding.

## Overlay rendering
- [ ] Every overlay `SetWindowPos(...HWND_TOPMOST...)` keeps the **1px virtual-screen inset**
      (`+1 / -2`) — no full virtual-screen rect (#44755).
- [ ] Composition overlay creates DispatcherQueue + STA apartment before `Compositor`/`DesktopWindowTarget`.
- [ ] Coordinates are DPI-scaled (`RasterizationScale()`) for multi-monitor / mixed-DPI.
- [ ] Overlay window keeps `WS_EX_TRANSPARENT` (click-through) and doesn't participate in capture (VDI, #47242).

## Hooks & input
- [ ] Hook procs (`WH_MOUSE_LL`, `WH_KEYBOARD_LL`) do no blocking/slow work and call `CallNextHookEx`
      promptly (freeze risk, #48442).
- [ ] `SetWindowsHookEx` / `RegisterClassExW` / `RegisterRawInputDevices` return values checked; log
      `GetLastError` on failure (#44936).
- [ ] Raw-input snooping (`RIDEV_INPUTSINK`) is registered/removed with activation state (Find My Mouse
      `UpdateMouseSnooping`).

## Activation gestures / hotkeys
- [ ] An explicitly **cleared** shortcut is not overwritten by the built-in default (#48158).
- [ ] Game-mode / excluded-app gates preserved where they exist (Find My Mouse `StartSonar`).
- [ ] Gliding cursor: Escape cancel path intact; worker threads stopped on cancel; `SendInput`
      auto-click behavior unchanged unless intended.

## Settings UX (maintainer conventions, PR #48232)
- [ ] Single click does not produce a double effect.
- [ ] Mode-specific customization collapses/hides when the mode changes.
- [ ] Boolean options inside a `SettingsExpander` use a checkbox, not a toggle switch.

## Localization
- [ ] Activation-gesture text updated in BOTH the utility `resource.h` and the Settings-UI `.resw`
      (#45598, #46223).

## Build hygiene
- [ ] No new per-project `PlatformToolset`; uses `$(RepoRoot)` not `..\..\`. Any override is commented
      with a reason + follow-up (PR #44639).

## Tests
- [ ] Mouse Jump logic changes covered by `MouseJump.Common.UnitTests`.
- [ ] Activation/overlay/settings changes exercised by `MouseUtils.UITests`.
