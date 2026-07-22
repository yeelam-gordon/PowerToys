---
name: powertoys-alwaysontop-knowledge
description: 'PowerToys AlwaysOnTop module knowledge: feature->file/function map, recurring regression playbooks (system-menu integration breaking custom-titlebar apps, menu command-ID collision/de-dup, settings file-watcher race, opacity hotkey conflicts/numpad/localized layouts, transparency layered-window restore mismatch, border null-deref, elevated-window limits, C++/C# default drift), maintainer review rules, and Pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/alwaysontop — pin/unpin topmost, WS_EX_TOPMOST, hotkeys, opacity/transparency, colored border frame, rounded corners, virtual desktop, system menu, excluded apps, game mode, settings. Keywords: AlwaysOnTop, pin window, topmost, HWND_TOPMOST, system menu, opacity, transparency, layered window, WinEvent hook, DWM border, DPI, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys AlwaysOnTop Knowledge

Grounded engineering knowledge for the PowerToys **AlwaysOnTop** module — pins the foreground
window above all others (`WS_EX_TOPMOST`), draws a configurable colored border around pinned
windows, adjusts per-window opacity, and optionally adds a toggle to the window's system (title-bar
right-click) menu. Use it to localize code fast, avoid known regression traps, and enforce the
conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/alwaysontop/` and needing prior art.
- Fixing/triaging an AlwaysOnTop bug: pin/unpin not working, border missing/garbled, opacity keys
  dead, system-menu toggle breaking another app's title-bar menu, settings not applied live, crash
  on border refresh, doesn't work on elevated windows.
- Reviewing an AlwaysOnTop PR against maintainer conventions and regression traps.
- Touching hotkey registration, the transparency/layered-window path, the DWM border, virtual-desktop
  tracking, the system-menu integration, or the settings snapshot/observer plumbing.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Process entry, single-instance mutex, GPO disable gate | `AlwaysOnTop/main.cpp` `wWinMain` (`instanceMutexName`, `getConfiguredAlwaysOnTopEnabledValue`) |
| Runner-side enable/disable, spawn `PowerToys.AlwaysOnTop.exe` | `AlwaysOnTopModuleInterface/dllmain.cpp` `Enable`/`Disable` (ShellExecuteEx + terminate events) |
| Runner hotkey plumbing (pin/opacity), settings parse, GPO | `dllmain.cpp` `on_hotkey`, `get_hotkeys`, `parse_hotkey`, `init_settings`, `gpo_policy_enabled_configuration` |
| Central instance lifecycle, DPI-aware init, subscribe | `AlwaysOnTop/AlwaysOnTop.cpp` ctor `AlwaysOnTop::AlwaysOnTop`, `InitMainWindow` |
| Pin / unpin core | `AlwaysOnTop.cpp` `ProcessCommand`, `PinTopmostWindow` (`SetWindowPos HWND_TOPMOST` + `SetProp AlwaysOnTop_Pinned`), `UnpinTopmostWindow`, `IsTopmost`, `IsPinned` |
| Hotkey handling (window-registered) | `AlwaysOnTop.cpp` `RegisterHotkey` (`RegisterHotKey`), `WndProc` `WM_HOTKEY`; `HotkeyId` enum |
| Centralized low-level KB hook path | `AlwaysOnTop.cpp` `RegisterLLKH` (named events + worker thread `MsgWaitForMultipleObjects` on 4 handles) |
| Opacity / transparency | `AlwaysOnTop.cpp` `StepWindowTransparency`, `ApplyWindowAlpha`, `RestoreWindowAlpha`, `ResolveTransparencyTargetWindow`; cache `m_windowOriginalLayeredState` |
| System-menu ("Always on top" toggle in title-bar menu) | `AlwaysOnTop.cpp` `UpdateSystemMenuItem`, `UpdateSystemMenuEventHooks`, `HandleWinHookEvent`; ownership check `IsAlwaysOnTopMenuCommand` (dwItemData owner tag `0x414F5450`) |
| Window event subscriptions | `AlwaysOnTop.cpp` `SubscribeToEvents` (`SetWinEventHook` for LOCATIONCHANGE/MINIMIZE/MOVESIZEEND/FOREGROUND/DESTROY/FOCUS), `HandleWinHookEvent` |
| Excluded apps / game-mode block | `AlwaysOnTop.cpp` `isExcluded` (`check_excluded_app`); `ProcessCommand` `blockInGameMode` + `detect_game_mode` |
| Border/frame window (per pinned window) | `AlwaysOnTop/WindowBorder.cpp` `Create`/`Init`/`UpdateBorderPosition`/`UpdateBorderProperties`; 100 ms `WM_TIMER` refresh; `GetFrameRect` (`DWMWA_EXTENDED_FRAME_BOUNDS`) |
| Border rendering (Direct2D) | `AlwaysOnTop/FrameDrawer.cpp` `SetBorderRect`, `Render`, `ConvertColor` |
| Rounded-corner radius | `AlwaysOnTop/WindowCornersUtil.cpp` `CornersRadius` (`DWMWA_WINDOW_CORNER_PREFERENCE`) |
| DPI scaling factor | `AlwaysOnTop/ScalingUtils.cpp` `ScalingFactor` |
| Virtual-desktop tracking | `AlwaysOnTop/VirtualDesktopUtils.cpp` `IsWindowOnCurrentDesktop`, `GetDesktopId`; `AssignBorder`/`RefreshBorders` |
| Settings load, live file-watch, observer notify | `AlwaysOnTop/Settings.cpp` `LoadSettings`, `InitFileWatcher`, `NotifyObservers`, `HexToRGB`; atomic snapshot `AlwaysOnTopSettings::settings()` |
| Settings schema / defaults (C++) | `AlwaysOnTop/Settings.h` `struct Settings`; ids `AlwaysOnTop/SettingsConstants.h` `enum SettingId`; observer base `SettingsObserver.h` |
| Settings UI (C#) — must mirror C++ defaults | `src/settings-ui/.../Views/AlwaysOnTopPage.xaml`, `ViewModels/AlwaysOnTopViewModel.cs`, `Settings.UI.Library/AlwaysOnTopProperties.cs` |
| Telemetry | `AlwaysOnTop/trace.cpp` `Trace::AlwaysOnTop::PinWindow`/`UnpinWindow`/`Enable` |

**Two independent "pinned" signals (keep consistent):** a window is topmost via `WS_EX_TOPMOST`
(`IsTopmost`) *and* tagged with the window property `AlwaysOnTop_Pinned` (`IsPinned`). The runner-side
`dllmain.cpp` reads that property **by literal name** (`GetPropW(..., PinnedWindowProp)`), so the
string must stay in sync between `AlwaysOnTop.cpp` and `dllmain.cpp`.

## Regression Playbooks

Rule by rule: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### System-menu integration breaks other apps' title-bar menu
- **Symptom:** custom-titlebar apps (RDP/MSRDC, UWP, Firefox, custom Win32) lose or glitch their
  title-bar right-click menu; `TrackPopupMenu` returns `ERROR_INVALID_MENU_HANDLE (1401)`.
- **Where:** `UpdateSystemMenuItem` (mutates each window's `GetSystemMenu`), `HandleWinHookEvent`
  `EVENT_OBJECT_INVOKED`, `SubscribeToEvents`/`UpdateSystemMenuEventHooks` (system-wide hooks).
- **Root cause:** "Show in system menu" inserts/edits menu items on *foreign* windows and hooks
  `EVENT_OBJECT_INVOKED` process-wide; conflicts with apps that own/rebuild their system menu.
- **Guardrail:** keep `ShowInSystemMenu` opt-in (default **false**); only install menu hooks while it
  is enabled (`UpdateSystemMenuEventHooks`); verify item ownership before touch. Evidence: issues
  [#46483](https://github.com/microsoft/PowerToys/issues/46483),
  [#46569](https://github.com/microsoft/PowerToys/issues/46569),
  [#46808](https://github.com/microsoft/PowerToys/issues/46808),
  [#47058](https://github.com/microsoft/PowerToys/issues/47058),
  [#47917](https://github.com/microsoft/PowerToys/issues/47917),
  [#48006](https://github.com/microsoft/PowerToys/issues/48006); feature
  [PR #45773](https://github.com/microsoft/PowerToys/pull/45773).

### System-menu command-ID collision / duplicate item
- **Symptom:** toggle appears twice, or AlwaysOnTop clobbers another app's `SC_*`-range command.
- **Where:** `UpdateSystemMenuItem`, `IsAlwaysOnTopMenuCommand`.
- **Root cause:** a fixed system-menu command id (`0xEFE0`) reused without proving the existing item
  is ours.
- **Guardrail:** tag inserted items with `dwItemData = 0x414F5450` and verify via
  `IsAlwaysOnTopMenuCommand` before update/remove; if the id is present but not ours, **skip** and
  log. Evidence: [PR #45845](https://github.com/microsoft/PowerToys/pull/45845).

### Settings change not applied live / file-watcher race
- **Symptom:** editing settings while running is not picked up immediately; stale values read
  concurrently.
- **Where:** `Settings.cpp` `LoadSettings`/`InitFileWatcher`; snapshot `AlwaysOnTopSettings::settings()`.
- **Root cause:** settings read on the worker thread while the file-watcher thread rewrites them.
- **Guardrail:** publish an immutable snapshot via
  `std::atomic<std::shared_ptr<const Settings>>` and **load once per operation** (do not call
  `settings()` repeatedly inside one command). Evidence: issue
  [#45993](https://github.com/microsoft/PowerToys/issues/45993); fix
  [PR #45994](https://github.com/microsoft/PowerToys/pull/45994).

### Opacity hotkey conflicts / numpad / localized layouts
- **Symptom:** opacity +/- collides on non-US layouts (e.g. CZE), numpad +/- do nothing, or the
  shortcut is hardcoded/undocumented.
- **Where:** `dllmain.cpp` `get_hotkeys`/`parse_hotkey`; `Settings` `increaseOpacityHotkey`/
  `decreaseOpacityHotkey` (`VK_OEM_PLUS`/`VK_OEM_MINUS`).
- **Root cause:** opacity reused the pin modifiers with hardcoded `VK_OEM_PLUS/MINUS`; numpad keys are
  different virtual-key codes.
- **Guardrail:** expose increase/decrease-opacity as **independently configurable** hotkeys;
  `get_hotkeys` must fill `min(buffer_size, count)` and set `isShown = (key != 0)` so cleared
  shortcuts aren't surfaced as active. Evidence: issues
  [#46135](https://github.com/microsoft/PowerToys/issues/46135),
  [#46300](https://github.com/microsoft/PowerToys/issues/46300),
  [#46391](https://github.com/microsoft/PowerToys/issues/46391); fix
  [PR #46410](https://github.com/microsoft/PowerToys/pull/46410).

### Transparency not restored on unpin / leaked WS_EX_LAYERED
- **Symptom:** unpinning doesn't restore the window's original opacity; window left `WS_EX_LAYERED`.
- **Where:** `ApplyWindowAlpha`, `RestoreWindowAlpha`, `ResolveTransparencyTargetWindow`; cache
  `m_windowOriginalLayeredState` (keyed by HWND).
- **Root cause:** apply/restore keyed inconsistently, and cache is erased even when a
  `SetLayeredWindowAttributes`/`SetWindowLong` call fails — original state is then unrecoverable.
- **Guardrail:** only adjust pinned windows; cache the original layered state on first change and
  restore keyed to the **same** window; don't drop the cache on a failed restore. Evidence:
  [PR #44815](https://github.com/microsoft/PowerToys/pull/44815) review thread.

### Null FrameDrawer deref during border refresh
- **Symptom:** crash inside the border's 100 ms timer refresh.
- **Where:** `WindowBorder::UpdateBorderPosition`.
- **Root cause:** `m_frameDrawer` used without a null check after teardown/failed init.
- **Guardrail:** guard `m_trackingWindow && m_frameDrawer && m_window` before use. Evidence:
  [PR #48412](https://github.com/microsoft/PowerToys/pull/48412).

### Doesn't work on elevated windows
- **Symptom:** pin / border / system-menu toggle have no effect on apps running as administrator.
- **Where:** whole pin path (`SetWindowPos`, `SetProp`, WinEvent hooks) targeting elevated HWNDs.
- **Root cause:** a non-elevated process cannot manipulate or hook elevated windows (UIPI).
- **Guardrail:** known limitation — document "run PowerToys as admin"; don't silently no-op without a
  diagnostic. Evidence: issues
  [#46775](https://github.com/microsoft/PowerToys/issues/46775),
  [#47549](https://github.com/microsoft/PowerToys/issues/47549).

## Review Rules

Enforce these when reviewing or authoring AlwaysOnTop changes:

- **Load the settings snapshot once per operation.** `AlwaysOnTopSettings::settings()` does an atomic
  load each call and can observe different snapshots mid-reload; bind a local at the top of
  `ProcessCommand`/`StepWindowTransparency`. Evidence:
  [PR #45994 review](https://github.com/microsoft/PowerToys/pull/45994).
- **Never mutate a foreign window's system menu without ownership proof.** Tag with `dwItemData`
  (`0x414F5450`) and check `IsAlwaysOnTopMenuCommand` before insert/update/remove. Evidence
  [#45845](https://github.com/microsoft/PowerToys/pull/45845), [#47917](https://github.com/microsoft/PowerToys/issues/47917).
- **Install global `SetWinEventHook`s only while needed.** `EVENT_OBJECT_INVOKED` fires system-wide
  very frequently; add/remove menu hooks in response to `SettingId::ShowInSystemMenu`
  (`UpdateSystemMenuEventHooks`), don't hook unconditionally. Evidence:
  [PR #45773 review](https://github.com/microsoft/PowerToys/pull/45773).
- **Honor the `get_hotkeys` contract.** Fill `min(buffer_size, hotkeyCount)`; set
  `isShown = (key != 0)` so disabled shortcuts aren't reported as active to the conflict manager.
  Evidence: [PR #46410 review](https://github.com/microsoft/PowerToys/pull/46410).
- **Check return values on window-state Win32 calls.**
  [`RegisterHotKey`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey),
  `SetWindowPos`, `SetLayeredWindowAttributes`, `SetWindowLong` can fail and leave inconsistent
  state; log on failure. Evidence: [PR #44815 review](https://github.com/microsoft/PowerToys/pull/44815).
- **Keep C++ and C# defaults in lockstep.** `Settings.h` (comment: *"kept in sync with
  AlwaysOnTopProperties.cs"*) and `AlwaysOnTopProperties.cs` must agree — the default frame color
  currently diverges (`#00ADEF` vs `#0099cc`). Evidence:
  [#46961](https://github.com/microsoft/PowerToys/issues/46961).
- **Don't pass `string_view::data()` to APIs needing a null-terminated string.**
  `HexToRGB` calls `std::stoll(hex.data())` on a
  [`std::wstring_view`](https://en.cppreference.com/w/cpp/string/basic_string_view/data) (not
  guaranteed null-terminated) — construct a `std::wstring` first. Evidence:
  [#46962](https://github.com/microsoft/PowerToys/issues/46962).
- **Don't reorder `Microsoft.Cpp.*.props` imports; use `$(RepoRoot)` not `..\..\`.** Include order is
  sensitive across all vcxproj. Evidence:
  [PR #44639 review](https://github.com/microsoft/PowerToys/pull/44639).
- **Ship a test for new persisted settings.** Settings-UI serialization/CLI `set` tests should cover
  every new property (e.g. `ShowInSystemMenu`). Evidence:
  [PR #45773 review](https://github.com/microsoft/PowerToys/pull/45773).

## Pitfalls

- **`ShowInSystemMenu` is the #1 "breaks other apps" trap.** It is opt-in (default false) precisely
  because enabling it hooks `EVENT_OBJECT_INVOKED` system-wide and edits foreign windows' system
  menus; treat any change here as high-blast-radius (#47917, #46808, #47058).
- **Never assume the transparency target equals the pinned window.**
  `ResolveTransparencyTargetWindow` may return a root/owner; apply and restore must use the same key
  or opacity leaks on unpin (#44815).
- **Elevated windows require elevated PowerToys** — pin/border/menu silently no-op otherwise
  (#46775, #47549).
- **Two pin signals must stay consistent:** `WS_EX_TOPMOST` and the `AlwaysOnTop_Pinned` window prop;
  `dllmain.cpp` reads the prop by literal name, so renaming it in one place breaks the runner path.
- **The LLKH worker waits on 4 named-event handles** via `MsgWaitForMultipleObjects`; a failed
  `CreateEventW` leaves a null handle in the array and corrupts the wait — fail fast if a critical
  event can't be created (#44815).
- **The border is a separate layered, topmost tool window** refreshed every 100 ms by `WM_TIMER`;
  it re-reads DWM frame bounds each tick — mind cost with many pinned windows.
- **Numpad `+`/`-` are not `VK_OEM_PLUS`/`VK_OEM_MINUS`.** Opacity shortcuts bound to the OEM keys
  won't fire from the numeric keypad (#46300).
- **`WindowCornerUtils::CornersRadius` is Windows-11 only** — `DWMWA_WINDOW_CORNER_PREFERENCE`
  returns `E_INVALIDARG` on Windows 10 (handled quietly); rounded corners degrade to square.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**; then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you on recurring
themes and measurably lowers your catch rate on the PR's actual issues. If a symptom doesn't map to
a row, reason from the source, not the map. Best for planning / triage; a targeted checklist (not a
script) for review.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to an AlwaysOnTop PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/alwaysontop/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/alwaysontop)
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) · [Layered windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows) · [DWMWA_WINDOW_CORNER_PREFERENCE](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute) · [string_view::data](https://en.cppreference.com/w/cpp/string/basic_string_view/data)
