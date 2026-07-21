---
name: powertoys-grabandmove-knowledge
description: 'PowerToys GrabAndMove module knowledge: feature->file/function map, recurring regression playbooks (modifier absorb/replay & stuck keys, held-key counter, cross-thread g_excludedCache data race, shell-surface/desktop exclusion, maximized drag/resize anchoring, remote-session overlay corners, CppWinRT LNK2038 CI break), maintainer review rules, and gotchas. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/GrabAndMove — Alt/Win+drag to move, Alt+right-drag to resize, low-level keyboard/mouse hooks, overlay rendering, window exclusion, settings. Keywords: GrabAndMove, Alt drag, move resize window, low-level hook, WH_KEYBOARD_LL, WH_MOUSE_LL, WinEventProc, overlay, GDI+, excluded apps, game mode, CppWinRT, LNK2038, PR review, regression, data race.'
license: Complete terms in LICENSE.txt
---

# PowerToys GrabAndMove Knowledge

Grounded engineering knowledge for the PowerToys **GrabAndMove** module — a standalone utility
(`PowerToys.GrabAndMove.exe`) that lets the user move a window with **modifier + left-drag** and
resize it with **modifier + right-drag** (modifier is Alt or Win), anywhere on the window, with a
semi-transparent overlay preview. It is implemented almost entirely as global **low-level keyboard
and mouse hooks** plus a **WinEvent** foreground hook, so most bugs are about **hook state,
modifier key absorption/replay, thread-safety, and target-window filtering** — not UI. Use this to
localize code fast, avoid known regression traps, and enforce maintainer conventions.

## When to Use This Skill

- Planning or implementing a change under `src/modules/GrabAndMove/` and needing prior art.
- Fixing/triaging a GrabAndMove bug: Alt/Win key gets "stuck" or stops working in another app;
  modifier shortcuts break; the wrong window (desktop, taskbar, Start menu, Command Palette) gets
  dragged; resize prefers one axis; maximized-window drag jumps; overlay corners look wrong over RDP.
- Reviewing a GrabAndMove PR against maintainer conventions and the concurrency traps below.
- Touching the low-level hooks (`KeyboardProc`/`MouseProc`), the modifier absorb/replay logic, the
  `IsExcluded`/`IsSystemClass` filters, overlay rendering, or the settings watcher thread.
- Adding a new native `.vcxproj` or bumping the toolset (CppWinRT / MSVC) — this module has already
  broken PowerToys CI on a version mismatch.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring
below). All paths under `src/modules/GrabAndMove/`.

| Sub-feature | Implementation (file · function) |
|---|---|
| Module lifecycle (launch/kill the exe, enable/disable, GPO gate) | `GrabAndMoveModuleInterface/dllmain.cpp` `GrabAndMoveInterface::enable/disable/get_config/set_config`, `gpo_policy_enabled_configuration` → `getConfiguredGrabAndMoveEnabledValue()` |
| Settings serialization to the module process | `dllmain.cpp::set_config` writes JSON, signals `GRABANDMOVE_REFRESH_SETTINGS_EVENT`; exit via `GRABANDMOVE_EXIT_EVENT` |
| Process entry point, single-instance mutex, hook install | `GrabAndMove/main.cpp` `wWinMain` (mutex `Local\PowerToys_GrabAndMove_InstanceMutex`) |
| Low-level keyboard hook (modifier absorb/replay, held-key tracking) | `main.cpp::KeyboardProc` (`WH_KEYBOARD_LL`) |
| Low-level mouse hook (start/track/end drag & resize, click swallow) | `main.cpp::MouseProc` (`WH_MOUSE_LL`) |
| Modifier absorb + replay (swallow Alt/Win, replay if no drag) | `main.cpp` `ReplayAbsorbedModifier`, `g_altAbsorbed/g_winAbsorbed` (must be reset together with the paired `g_altPressed`/`g_winPressed` on every non-drag release path, incl. a non-Alt key arriving while Alt is absorbed — else the modifier stays internally "stuck", PR #47261), `g_absorbedVk/ScanCode/Flags` |
| Held-non-modifier-key counter (suppress activation when a key is down) | `main.cpp::KeyboardProc` `g_keyHeld[256]`, `g_heldNonAltKeyCount` |
| Foreground-change reset (unstick keys, invalidate cache) | `main.cpp::WinEventProc` (`EVENT_SYSTEM_FOREGROUND`, `WINEVENT_OUTOFCONTEXT`) |
| Move drag math (incl. maximized-restore anchoring) | `main.cpp::HandleDragMove`; commit on `WM_LBUTTONUP` in `MouseProc` |
| Resize drag math (handles, min size, maximized-restore) | `main.cpp::HandleDragResize`, `GetClosestHandle`, `CursorForHandle`, `MIN_WINDOW_WIDTH/HEIGHT` |
| Target window resolution (root, remote-session foreground) | `main.cpp::ResolveTargetWindow` |
| Window exclusion — system/shell classes | `main.cpp::IsSystemClass` (Progman, Shell_TrayWnd, Task View, flyouts, …) |
| Window exclusion — shell CoreWindow by process + user excluded_apps | `main.cpp::IsExcluded` (+ `g_excludedApps` snapshot, `g_excludedCache` HWND→bool) |
| Overlay window (semi-transparent preview, geometry text) | `main.cpp` `EnsureOverlayWindow`, `ShowOverlay`, `RepositionOverlay`, `HideOverlay`, `RenderOverlayContent` |
| Overlay border / rounded corners (DWM corner pref, remote = square) | `main.cpp` `DrawOverlayBorder`, `PrepareOverlayMetrics`, `CornerRadiusForWindow` (GDI+) |
| Game Mode suppression | `main.cpp::IsSuppressedByGameMode` (`detect_game_mode`, bypassed in remote sessions) |
| Settings load + hot-reload watcher thread | `main.cpp` `LoadSettingsFromFile`, `SettingsWatcherThread`; keys: `shouldAbsorbAlt`, `showGeometry`, `doNotActivateOnGameMode`, `useAltResize`, `modifierKey`, `excluded_apps` |
| Tray icon + menu | `main.cpp` `AddTrayIcon`, `RemoveTrayIcon`, `ShowTrayMenu` |
| Telemetry | `main.cpp::TraceShortcutUse`; `GrabAndMoveModuleInterface/trace.cpp` |
| Settings UI (view model + properties) | `src/settings-ui/Settings.UI/ViewModels/GrabAndMoveViewModel.cs`, `src/settings-ui/Settings.UI.Library/GrabAndMoveProperties.cs`, `src/settings-ui/Settings.UI.Library/GrabAndMoveSettings.cs` |
| CppWinRT NuGet pin (build) | `GrabAndMove/GrabAndMove.vcxproj` imports `packages\Microsoft.Windows.CppWinRT.2.0.250303.1\...` |

**Modifier value mapping (keep C++ and C# in sync):** the native side is
`enum class GrabAndMoveModifier { Alt = 0, Win = 1 }`; `LoadSettingsFromFile` reads int `modifierKey`
(`1 → Win`, else `Alt`). The C# view model uses raw `int` 0/1 across the settings boundary (a maintainer
deliberately kept it an int, not an enum, for a two-value bugfix — see Review Rules).

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md). Issues/PRs are on
`microsoft/PowerToys`.

### Cross-thread data race on shared hook state / `g_excludedCache`
- **Symptom:** rare crash or memory corruption; excluded-apps list not taking effect / stale.
- **Where:** `main.cpp::IsExcluded` (`g_excludedCache` `unordered_map`), cleared from
  `LoadSettingsFromFile` (settings thread) and `WinEventProc` (foreground hook).
- **Root cause:** `std::unordered_map` `find()/operator[]` mutate the map; concurrent access from the
  settings watcher thread and the main/hook thread is a data race
  ([review on PR #47052](https://github.com/microsoft/PowerToys/pull/47052)).
- **Guardrail:** confine **all** `g_excludedCache` access to the main message-pump thread. The settings
  thread must `PostMessage(g_hMsgWnd, WM_INVALIDATE_EXCLUDED_CACHE, …)` instead of clearing directly
  (fixed in commit `ea37c3a`). Share the excluded-apps *list* via an immutable snapshot in
  `std::atomic<shared_ptr<const vector<wstring>>> g_excludedApps`, never a mutable container.
  See [C++ data race / atomic shared_ptr](https://en.cppreference.com/w/cpp/memory/shared_ptr/atomic2).

### Modifier key gets "stuck" / stops responding
- **Symptom:** after using Alt/Win (or Win+L, Alt+Tab, a game), the modifier no longer triggers
  GrabAndMove, or a plain key (e.g. `G` summoning Game Bar) becomes unresponsive until re-pressed.
- **Where:** `main.cpp::KeyboardProc` held-key tracking; `main.cpp::WinEventProc` reset.
- **Root cause:** a swallowed key-up (e.g. Win+L eats the `L` keyup before the session locks) left
  `g_heldNonAltKeyCount > 0`, permanently suppressing interception. An earlier version misread
  `KF_REPEAT` off `KBDLLHOOKSTRUCT::flags` — **that bit does not exist on the LL hook struct**, so
  auto-repeat inflated the counter forever
  ([review on PR #47052](https://github.com/microsoft/PowerToys/pull/47052)).
- **Guardrail:** track held keys by **per-vkCode transition** (`g_keyHeld[256]`: increment only on
  false→true, decrement only on true→false), never via `KF_REPEAT`. Reset **both**
  `g_heldNonAltKeyCount` and `g_keyHeld` in `WinEventProc` on every foreground change. Evidence: fixes
  [PR #47052](https://github.com/microsoft/PowerToys/pull/47052); open reports
  [#48190](https://github.com/microsoft/PowerToys/issues/48190),
  [#47802](https://github.com/microsoft/PowerToys/issues/47802),
  [#49037](https://github.com/microsoft/PowerToys/issues/49037).

### Absorbed Alt stays "stuck" (internal `g_altPressed`) when another key is pressed
- **Symptom:** Alt is held to arm GrabAndMove, the user presses another key (Shift/Ctrl/a letter —
  anything but the allowed mouse interactions or Tab) without dragging, and afterwards GrabAndMove
  behaves as if Alt is still held (modifier "stuck"), so subsequent interactions misfire.
- **Where:** `main.cpp::KeyboardProc`, the `else` branch that handles a **non-Alt** key while Alt was
  absorbed without a drag: `if (g_altAbsorbed && !g_dragConsumedAlt) { ... ReplayAbsorbedModifier(false); }`.
- **Root cause:** that branch cleared the *absorb* flag (`g_altAbsorbed = false`) and replayed the real
  Alt keydown to the app, but did **not** clear the *pressed* flag `g_altPressed`. The two flags are a
  pair — `g_altAbsorbed` = "we swallowed Alt", `g_altPressed` = "our state thinks Alt is down". Leaving
  `g_altPressed == true` after releasing/replaying Alt left GrabAndMove's internal state believing Alt
  was still held, i.e. a stuck modifier.
- **Guardrail:** any path that ends the absorbed-Alt state without a drag must reset **both** flags —
  set `g_altPressed = false` alongside `g_altAbsorbed = false` before `ReplayAbsorbedModifier(false)`
  (mirrors the modifier-keyup path which already clears `g_altPressed`). The mouse/Win keys were never
  affected. Evidence: fix [PR #47261](https://github.com/microsoft/PowerToys/pull/47261) (closes
  [#47257](https://github.com/microsoft/PowerToys/issues/47257)).

### Absorbed Alt/Win breaks the modifier's normal use in other apps
- **Symptom:** with "absorb Alt" on, tapping Alt no longer opens app menus; Win no longer opens Start;
  or a foreign Win/Alt keyboard shortcut (Win+Shift, Alt+Scroll) stops working.
- **Where:** `main.cpp::KeyboardProc` (swallow keydown, `return 1`), `main.cpp::ReplayAbsorbedModifier`.
- **Root cause:** GrabAndMove swallows the modifier keydown to prevent the menu/Start flash, but must
  **replay** the original keydown (and sometimes keyup) when no drag/resize actually consumed it.
  Missing/one-sided replay leaves the modifier's normal behavior broken.
- **Guardrail:** on modifier keyup with `!wasDragging && !wasResizing && !g_dragConsumedAlt`, replay
  the absorbed key; replay **both** down and up for Win (Start opens on keyup, so a lone replayed
  keydown isn't enough). Also replay when a **non-modifier** key arrives while the modifier is absorbed.
  Evidence: [PR #47261](https://github.com/microsoft/PowerToys/pull/47261) (release Alt on other press),
  [PR #47326](https://github.com/microsoft/PowerToys/pull/47326) (replay Win down+up); reports
  [#47585](https://github.com/microsoft/PowerToys/issues/47585),
  [#47787](https://github.com/microsoft/PowerToys/issues/47787),
  [#47774](https://github.com/microsoft/PowerToys/issues/47774),
  [#48121](https://github.com/microsoft/PowerToys/issues/48121).

### Wrong target: desktop icons, taskbar, Start menu, Command Palette get dragged
- **Symptom:** Alt+drag on the desktop drags icons / the wallpaper; Start menu, Search, Quick Settings,
  Widgets, or PowerToys Command Palette get moved instead of being ignored.
- **Where:** `main.cpp::ResolveTargetWindow` → `IsSystemClass` (class-name filter) and `IsExcluded`
  (shell `Windows.UI.Core.CoreWindow`/`ControlCenterWindow`/`WindowsDashboard` filtered **by process
  path**, plus user `excluded_apps`).
- **Root cause:** shell surfaces share generic window classes (`CoreWindow`), so a class-only filter
  is insufficient; must resolve `GetAncestor(GA_ROOT)` and reject shell processes
  (`STARTMENUEXPERIENCEHOST.EXE`, `SHELLEXPERIENCEHOST.EXE`, `SEARCHHOST.EXE`, `SHELLHOST.EXE`,
  `WIDGETBOARD.EXE`).
- **Guardrail:** when a new "unexpected window is draggable" report arrives, add the surface to
  `IsSystemClass` (by class) or `IsExcluded` (by class + process path) — follow the documented
  discovery recipe in the `IsExcluded` comment. Evidence:
  [PR #47302](https://github.com/microsoft/PowerToys/pull/47302) (skip desktop/explorer targets);
  reports [#47926](https://github.com/microsoft/PowerToys/issues/47926),
  [#48056](https://github.com/microsoft/PowerToys/issues/48056),
  [#47832](https://github.com/microsoft/PowerToys/issues/47832),
  [#48081](https://github.com/microsoft/PowerToys/issues/48081) (Command Palette),
  [#47667](https://github.com/microsoft/PowerToys/issues/47667).

### Maximized-window drag/resize jumps away from the cursor
- **Symptom:** grabbing a maximized window restores it but the window jumps so the cursor is no longer
  over the grab point; horizontal move "unlocks" a vertically-maximized window oddly.
- **Where:** `main.cpp::HandleDragMove` / `HandleDragResize` first-move maximized branch (`IsZoomed`
  → `SW_RESTORE`).
- **Root cause:** after `SW_RESTORE` the window size changes, so the new position must be re-anchored
  **proportionally to the click point** using the **current** cursor position, not the stale
  `g_dragStart`. A review flagged the move path anchoring to `g_dragStart` instead of `pt`, unlike the
  resize path.
- **Guardrail:** compute the restore offset from the click ratio and anchor to the current `pt`; keep
  `HandleDragMove` and `HandleDragResize` consistent. Evidence:
  [PR #49118](https://github.com/microsoft/PowerToys/pull/49118) + its review comment; report
  [#49123](https://github.com/microsoft/PowerToys/issues/49123).

### Overlay corners wrong / hit-testing unstable over Remote Desktop
- **Symptom:** rounded overlay border with rounded-off corners over RDP (where windows are actually
  square); wrong window grabbed in a remote session.
- **Where:** `main.cpp::CornerRadiusForWindow`, `PrepareOverlayMetrics`, `ResolveTargetWindow`,
  `IsSuppressedByGameMode` — all branch on `GetSystemMetrics(SM_REMOTESESSION)`.
- **Root cause:** remote sessions draw square windows yet still report `DWMWCP_DEFAULT`, and
  `WindowFromPoint` hit-testing is unstable for topmost windows.
- **Guardrail:** in remote sessions force square corners (radius 0), prefer the foreground top-level
  window for target resolution, and don't suppress on Game Mode (fullscreen-notification false
  positives). Evidence: [PR #48999](https://github.com/microsoft/PowerToys/pull/48999).

### CppWinRT / MSVC toolset mismatch → `LNK2038` (breaks PowerToys CI)
- **Symptom:** `LNK2038: mismatch detected for 'C++/WinRT version'` linking
  `PowerToys.GrabAndMove.exe`; whole CI leg blocked.
- **Where:** `GrabAndMove/GrabAndMove.vcxproj` NuGet imports.
- **Root cause:** the vcxproj didn't import the pinned `Microsoft.Windows.CppWinRT` NuGet, so `main.cpp`
  resolved `<winrt/...>` from the in-box Windows SDK CppWinRT while `SettingsAPI.lib` used the pinned
  NuGet version — a mismatch surfaced by an agent-image MSVC bump (`14.50` → `14.51`).
- **Guardrail:** any native GrabAndMove `.vcxproj` must mirror the canonical CppWinRT NuGet wiring used
  by every other native project (`packages\Microsoft.Windows.CppWinRT.2.0.250303.1\...` via
  `$(RepoRoot)`, with `packages.config`). Evidence:
  [PR #47910](https://github.com/microsoft/PowerToys/pull/47910).

## Review Rules

Enforce these when reviewing or authoring GrabAndMove changes:

- **Confine hook/shared state to one thread.** `g_excludedCache` and other hook globals must be touched
  only on the main message-pump thread; the settings watcher marshals via
  `PostMessage(WM_INVALIDATE_EXCLUDED_CACHE)`. Never mutate a shared `unordered_map` from the settings
  thread. Cross-process/threaded config → immutable snapshot in `atomic<shared_ptr<const …>>`.
- **Track held keys by transition, never `KF_REPEAT`.** `KBDLLHOOKSTRUCT::flags` carries no
  `KF_REPEAT` bit; use the `g_keyHeld[256]` up→down/down→up model.
- **Every swallowed modifier needs a replay path.** If `KeyboardProc` `return 1`s on a modifier
  keydown, ensure `ReplayAbsorbedModifier` runs when no drag consumed it (both down+up for Win).
- **Ignore injected events.** Bail on `LLKHF_INJECTED` / `LLMHF_INJECTED` at the top of the hooks — the
  module replays keys and must not re-process its own injected input (feedback loop).
- **Filter new shell surfaces by class *and* process.** Generic classes like
  `Windows.UI.Core.CoreWindow` are shared; add exclusions in `IsSystemClass`/`IsExcluded` using the
  documented discovery recipe, not ad-hoc `WindowFromPoint` guesses.
- **Keep move/resize maximized-restore anchoring consistent** and anchored to the live cursor `pt`.
- **Resize only resizable windows.** Gate resize on `GetWindowLongW(hwnd, GWL_STYLE) & WS_THICKFRAME`;
  respect `MIN_WINDOW_WIDTH/HEIGHT`.
- **Include STL headers explicitly in `pch.h`.** `main.cpp` uses `std::atomic`; `pch.h` must
  `#include <atomic>` — don't rely on transitive includes (they break silently on toolset changes).
- **Keep the C++/C# modifier mapping in sync** (`Alt=0`, `Win=1`). A maintainer intentionally kept the
  C# side a raw `int` for a two-value bugfix — don't churn it into an enum inside an unrelated PR
  ([review on PR #47052](https://github.com/microsoft/PowerToys/pull/47052)).
- **New native `.vcxproj` mirrors the canonical CppWinRT NuGet wiring** (see the CI playbook).
- **Prefer vcpkg + a patch file over vendored shim headers.** A maintainer called the
  `deps/spdlog-msvc-fix` shim (historical — path as of #47910; not in current tree) "slightly absurd"; toolset-compat fixes belong in the dependency, not a
  bespoke header ([review on PR #47910](https://github.com/microsoft/PowerToys/pull/47910)).
- **Plain comments.** A maintainer repeatedly pushed back on hyphenated compound-noun comments and
  "flowery" wording ("warning-gold", "literal equivalent"); keep code comments plain and factual.

## Gotchas

- **Never** touch `g_excludedCache` (or other hook globals) off the main message-pump thread — a
  `std::unordered_map` race there corrupts memory; marshal via `PostMessage`.
- **Never** read `KF_REPEAT` from `KBDLLHOOKSTRUCT::flags` — the bit isn't there; you'll leak the
  held-key counter and permanently suppress the modifier.
- **Never** swallow an Alt/Win keydown without a replay path — you'll break the modifier's normal use
  (Start menu, app menus, foreign shortcuts) system-wide.
- **Never** trust a class-name-only filter for shell surfaces — Start/Search/Quick Settings/Widgets all
  use `Windows.UI.Core.CoreWindow`; filter by process path too.
- **Always** bail on injected events first (`LLKHF_INJECTED`/`LLMHF_INJECTED`) or the module reprocesses
  its own replayed keys.
- **Remote Desktop is special-cased everywhere** — square corners, foreground-based target resolution,
  and Game Mode suppression off. Preserve the `SM_REMOTESESSION` branches when refactoring.
- **`WinEventProc` runs `WINEVENT_OUTOFCONTEXT`** — a maintainer confirmed it dispatches on the
  installing thread's message pump (same thread as the LL hooks), so it does **not** race the hook
  callbacks; the real race is settings-thread vs main-thread cache access. Don't "fix" a non-race.
- **Resize needs `WS_THICKFRAME`** and honors `MIN_WINDOW_WIDTH=150` / `MIN_WINDOW_HEIGHT=50`.

## Using This Skill in PR Review (Anti-Anchoring)

**Read the diff cold first.** Do not skim these playbooks and then hunt the diff for the same themes —
that anchors you on recurring concerns and lowers your catch rate on the PR's actual issues.

1. Read the diff and form your own list of concerns from what actually changed.
2. **Then** cross-check the touched files against the Module Map, Regression Playbooks, and Review
   Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat this file as a checklist for the touched area, not a script for the whole review.

When localizing a bug, if the symptom doesn't map cleanly to a row above, reason from the symptom and
verify in source — a thin/absent map entry can anchor you onto a confident, wrong file. This module is
new and its history is short, so gaps are expected.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a GrabAndMove PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/GrabAndMove/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/GrabAndMove)
- [Low-level keyboard hook / `KBDLLHOOKSTRUCT`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct) · [`SM_REMOTESESSION`](https://learn.microsoft.com/en-us/windows/win32/termserv/detecting-the-terminal-services-environment) · [`DWMWA_WINDOW_CORNER_PREFERENCE`](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)
