---
name: powertoys-fancyzones-knowledge
description: 'PowerToys FancyZones module knowledge: feature->file/function map for zone layouts, mouse drag-snap, keyboard (Win+arrow) snap, override-Windows-snap, multi-monitor/DPI, virtual-desktop layout binding, app-zone-history (last-known-zone restore), the WPF editor, and the CLI. Recurring regression playbooks (shutdown/teardown races in WorkArea/ZonesOverlay/OnThreadExecutor, stuck drag state + swallowed keys when a window is destroyed mid-drag, Win+Ctrl+Alt quick-layout digit stealing, Shift-only swallow during drag, last-known-zone moving all app windows, applied-layouts.json access-denied), maintainer review rules, and pitfalls. Load when planning, fixing, triaging, or reviewing changes under src/modules/fancyzones — zone snapping/dragging, layout apply/serialization, multi-monitor/virtual-desktop, window-position restore, editor spacing/highlight, CLI.'
license: Complete terms in LICENSE.txt
---

# PowerToys FancyZones Knowledge

Grounded engineering knowledge for the PowerToys **FancyZones** module — a window manager that
lets users define zone layouts and snap windows into them by dragging (with a modifier) or by
keyboard (Win+arrow / Win+Ctrl+Alt). It runs a host process driven by low-level mouse/keyboard
hooks and WinEvent hooks, renders zone overlays per monitor, persists layouts and per-app zone
history to JSON, binds layouts to monitors and virtual desktops, and ships a WPF editor plus a
CLI. Use this to localize code fast, avoid the recurring teardown-race / stuck-drag traps, and
enforce the conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/fancyzones/` and needing prior art.
- Fixing/triaging a FancyZones bug: overlays stay on screen, a drag gets stuck, number/Shift keys
  are swallowed, a window snaps to the wrong monitor/zone, layouts don't bind to a virtual desktop,
  windows aren't restored to their last zone, the host process crashes on display/monitor change.
- Reviewing a FancyZones PR against maintainer conventions and the teardown/hook regression traps.
- Touching drag/keyboard snap, the WorkArea/ZonesOverlay lifecycle, the hook dispatch in
  `FancyZones.cpp`, layout/zone-history JSON, monitor/virtual-desktop resolution, the editor, or CLI.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).
Native code lives in `FancyZonesLib/`; the editor is C#/WPF under `editor/FancyZonesEditor/`.

| Sub-feature | Implementation (file · function) |
|---|---|
| Host process / hook dispatch (mouse, keyboard, WinEvent) | `FancyZonesLib/FancyZones.cpp` `HandleWinHookEvent`, `WndProc`, `OnKeyDown` |
| Module DLL interface (enable/disable, settings, GPO, hotkeys) | `FancyZonesLib/FancyZones.cpp`; `FancyZonesModuleInterface/dllmain.cpp` |
| Drag start/update/end orchestration | `FancyZones.cpp` `MoveSizeStart` / `MoveSizeUpdate` / `MoveSizeEnd` |
| Mouse drag-to-snap (overlay, highlight, transparency) | `FancyZonesLib/WindowMouseSnap.cpp` `Create`, `MoveSizeStart/Update/End`, `Abort`; `FancyZonesLib/WindowMouseSnap.h` `GetDraggedWindow` |
| Keyboard snap (Win+arrow cycle, by-position, extend) | `FancyZonesLib/WindowKeyboardSnap.cpp` `Snap`, `Extend`, `MoveByDirectionAndIndex`, `SnapBasedOnPositionOnAnotherMonitor` |
| Dragging state (Shift/Ctrl toggle, active flag) | `FancyZonesLib/DraggingState.cpp` `Enable`, `Disable`, `UpdateDraggingState`, `IsDragging` |
| Override Windows Snap (Win+arrow interception) | `FancyZones.cpp` `ShouldProcessSnapHotkey` + `Settings.overrideSnapHotkeys` |
| Quick layout switch by digit while dragging/idle | `FancyZones.cpp` `OnKeyDown` (`changeLayoutWhileDragging` requires Win+Ctrl+Alt); `LayoutHotkeys.cpp` |
| Per-monitor work area (owns layout + overlay) | `FancyZonesLib/WorkArea.cpp`; config in `WorkAreaConfiguration.cpp` `Clear`, `GetAllWorkAreas` |
| Zone overlay rendering (D2D render thread) | `FancyZonesLib/ZonesOverlay.cpp` `RenderLoop`, `DrawActiveZoneSet`, `Show`/`Hide`/`Flash`; overlay window pool `NewZonesOverlayWindow`/`FreeZonesOverlayWindow` in `FancyZonesLib/WorkArea.cpp` (`WindowPool` class) |
| Highlighted-zone hit testing during drag | `FancyZonesLib/HighlightedZones.cpp` |
| Zone geometry / layout model | `FancyZonesLib/Zone.cpp`, `Layout.cpp`, `LayoutConfigurator.cpp`, `LayoutAssignedWindows.cpp` |
| Background worker (serialized tasks, teardown) | `FancyZonesLib/OnThreadExecutor.cpp` `submit`, `cancel`, `worker_thread`, dtor |
| Applied layouts (monitor→layout binding, JSON) | `FancyZonesLib/FancyZonesData/AppliedLayouts.cpp` |
| Custom / default / template layouts (JSON) | `FancyZonesLib/FancyZonesData/CustomLayouts.cpp`, `DefaultLayouts.cpp`, `LayoutTemplates.cpp` |
| Per-app zone history / last-known-zone restore | `FancyZonesLib/FancyZonesData/AppZoneHistory.cpp` `GetAppLastZoneIndexSet`, `SyncVirtualDesktops`, `AdjustWorkAreaIds` |
| Layout hotkeys (digit→layout) | `FancyZonesLib/FancyZonesData/LayoutHotkeys.cpp` |
| Monitor identification / DPI / span-across | `FancyZonesLib/MonitorUtils.cpp` (`namespace WMI`, `Display`) `IdentifyMonitors`; `Settings.spanZonesAcrossMonitors` |
| Virtual desktop id resolution (registry) | `FancyZonesLib/VirtualDesktop.cpp` `GetCurrentVirtualDesktopIdFromRegistry`, `GetVirtualDesktopIdsFromRegistry`; `LastUsedVirtualDesktop.cpp` |
| Window eligibility / processing rules | `FancyZonesLib/FancyZonesWindowProcessing.cpp`, `WindowUtils.cpp` |
| Window properties (zone stamp on HWND) | `FancyZonesLib/FancyZonesWindowProperties.cpp` |
| Settings model + observers | `FancyZonesLib/Settings.cpp/.h`, `SettingsObserver.h`, `SettingsConstants.h` |
| Editor launch parameters (monitor set handoff) | `FancyZonesLib/EditorParameters.cpp` |
| WPF editor (UI, view-models, layout math) | `editor/FancyZonesEditor/` `MainWindow.xaml.cs`, `ViewModels/`, `Models/`, `Utils/` |
| CLI (set-layout, GUID parsing, subcommand help) | `FancyZonesCLI/CommandLine/Commands/` |
| Unit / UI / fuzz tests | `FancyZonesTests/`, `FancyZonesEditor.UnitTests/`, `*.UITests/`, `FancyZones.FuzzTests/` |

**Hook dispatch (critical, `HandleWinHookEvent`):** FancyZones posts private messages from WinEvent
callbacks to its own window: `EVENT_SYSTEM_MOVESIZEEND`→`WM_PRIV_MOVESIZEEND`,
`EVENT_OBJECT_DESTROY`→`WM_PRIV_WINDOWDESTROYED`, etc. `OnKeyDown` returns **true to swallow** the
key from the foreground app. Swallowing is deliberately narrow: Win+arrow snap hotkeys, and only the
**bare** Shift key while dragging — never `Shift+<other>` (that steals app keystrokes).

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Shutdown/teardown races crash the host on display/monitor change
- **Symptom:** FancyZones host process crashes or hangs on display changes, monitor
  reconfiguration, a `SpanZonesAcrossMonitors` toggle mid-drag, or normal exit.
- **Where:** `ZonesOverlay.cpp` dtor; `WorkArea.cpp::~WorkArea`; `OnThreadExecutor.cpp` dtor;
  `FancyZones.cpp::UpdateWorkAreas` + `WorkAreaConfiguration::Clear`.
- **Root cause (four distinct):** (1) `~ZonesOverlay` unconditionally `join()`s `m_renderThread`,
  but the ctor can early-return (GetClientRect / CreateHwndRenderTarget failure during a display TDR)
  leaving the thread non-joinable → `std::terminate`. (2) `~WorkArea` returned the HWND to the pool
  **before** the render thread was torn down, so a recycled HWND got two render targets. (3)
  `~OnThreadExecutor` wrote `_shutdown_request` **outside** `_task_mutex`, so a notify racing the
  worker entering `_task_cv.wait` could be missed → `join()` hangs. (4) `WindowMouseSnap` held a
  dangling `WorkArea*`/`const&` across `WorkAreaConfiguration::Clear()` mid-drag → freed-pointer
  deref on the next `WM_MOUSEMOVE`.
- **Guardrail:** guard join with `if (m_renderThread.joinable())`; `m_zonesOverlay.reset()` **before**
  `FreeZonesOverlayWindow`; write the shutdown flag under `_task_mutex`; call `MoveSizeEnd()` (tears
  the snapper down; no-op if null) **before** each `m_workAreaConfiguration.Clear()`.
  Evidence: [PR #48473](https://github.com/microsoft/PowerToys/pull/48473).

### Stuck drag + swallowed number keys when a window is destroyed mid-drag
- **Symptom:** closing/destroying a window **while dragging** leaves zone overlays on screen and then
  swallows or incorrectly routes subsequent keystrokes (notably digits) from the focused app.
- **Where:** `FancyZones.cpp` `HandleWinHookEvent` (`EVENT_OBJECT_DESTROY`),
  `WM_PRIV_WINDOWDESTROYED` handler, `MoveSizeEnd`, `OnKeyDown`; `WindowMouseSnap::Abort`.
- **Root cause:** FancyZones never subscribed to `EVENT_OBJECT_DESTROY`, so the
  `WM_PRIV_WINDOWDESTROYED` branch never fired and the drag state stranded. With `dragging` stuck
  true, **any** digit switched layouts and stole number keys.
- **Guardrail:** subscribe to and route `EVENT_OBJECT_DESTROY`; on destroy of the dragged HWND call
  `WindowMouseSnap::Abort()` (NOT `MoveSizeEnd()` — that would snap a dead HWND and corrupt state)
  then disable dragging; **always** clear dragging state in `MoveSizeEnd()` even when the snapper was
  null; gate quick-layout switching (dragging or idle) behind **Win+Ctrl+Alt+digit**; swallow only
  the **bare** Shift during a drag. Evidence:
  [PR #48569](https://github.com/microsoft/PowerToys/pull/48569).

### Override Windows Snap adds unintended hotkeys / fights native snap
- **Symptom:** with "Override Windows Snap" on, Win+arrow snaps to native half-screen first, or extra
  unintended move hotkeys appear; sometimes FancyZones doesn't override at all.
- **Where:** `FancyZones.cpp::ShouldProcessSnapHotkey` + `OnKeyDown` swallow logic;
  `Settings.overrideSnapHotkeys`; `WindowKeyboardSnap.cpp`.
- **Root cause:** decision of *whether* to swallow Win+arrow (and which arrows) vs. let the OS handle
  it interacts with monitor topology and the "move based on relative position" setting.
- **Guardrail:** only swallow when `ShouldProcessSnapHotkey` confirms the foreground window is a snap
  candidate and the setting is on; keep swallow decision and actual snap consistent. Evidence (open):
  [#47580](https://github.com/microsoft/PowerToys/issues/47580),
  [#48387](https://github.com/microsoft/PowerToys/issues/48387),
  [#48048](https://github.com/microsoft/PowerToys/issues/48048).

### "Move to last known zone" collapses all app windows into one zone
- **Symptom:** with last-known-zone restore on, all windows of the same app pile into one zone; some
  apps (Adobe Illustrator/AE) open blank/black on multi-monitor.
- **Where:** `FancyZonesLib/FancyZonesData/AppZoneHistory.cpp::GetAppLastZoneIndexSet`; consumers in
  `FancyZones.cpp` new-window handling.
- **Root cause:** zone history is keyed per app (+ work-area id + layout id); multiple windows of one
  process resolve to the same saved index set, and multi-monitor work-area id resolution can route incorrectly.
- **Guardrail:** confirm the work-area/layout id used for lookup matches the target monitor; be wary
  of per-process (not per-window) history keys. Evidence (open):
  [#47010](https://github.com/microsoft/PowerToys/issues/47010),
  [#48234](https://github.com/microsoft/PowerToys/issues/48234),
  [#49209](https://github.com/microsoft/PowerToys/issues/49209).

### Layouts don't bind per virtual desktop / applied-layouts.json access denied
- **Symptom:** different layouts per Windows 11 "desktop" stop working after an update; or
  `Error applying layout: Access to the path 'applied-layouts.json' is denied`.
- **Where:** `FancyZonesLib/FancyZonesData/AppliedLayouts.cpp` (JSON read/write); `VirtualDesktop.cpp` registry id
  resolution; `AppZoneHistory::SyncVirtualDesktops`.
- **Root cause:** virtual-desktop GUIDs are read from the registry and can change/relocate across OS
  builds; concurrent/locked writes to the shared JSON cause access-denied.
- **Guardrail:** resolve the current VD id defensively (registry layout varies by build); serialize
  and error-handle JSON writes; migrate/sync ids on desktop changes. Evidence (open):
  [#49057](https://github.com/microsoft/PowerToys/issues/49057),
  [#48374](https://github.com/microsoft/PowerToys/issues/48374).

### CLI: `{GUID}` swallowed by PowerShell as a script block
- **Symptom:** `FancyZonesCLI set-layout {GUID}` fails cryptically in PowerShell.
- **Where:** `FancyZonesCLI/CommandLine/Commands/` GUID parsing.
- **Root cause:** PowerShell interprets `{...}` as a script block before the CLI sees it.
- **Guardrail:** accept brace-less GUIDs; detect the script-block case and print a friendly message;
  add per-subcommand `--help`. Evidence:
  [PR #44676](https://github.com/microsoft/PowerToys/pull/44676) (closes #44633, #44675).

## Review Rules

Enforce these when reviewing or authoring FancyZones changes:

- **Every teardown/reconfiguration path must be race-safe.** Guard `thread::join()` with `joinable()`; stop
  the render thread before recycling its HWND; write shutdown flags under the same mutex the waiter
  uses; tear down the drag snapper before clearing the work-area map (PR #48473).
- **Never dereference a `WorkArea*`/`const&` across `WorkAreaConfiguration::Clear()`.** Monitor
  changes and `SpanZonesAcrossMonitors` rebuild the map mid-session; call `MoveSizeEnd()` first
  (PR #48473).
- **Abort — don't end — a drag whose window died.** On `WM_PRIV_WINDOWDESTROYED` for the dragged
  HWND, use `WindowMouseSnap::Abort()`; `MoveSizeEnd()` would snap a dead window and corrupt state
  (PR #48569).
- **Always clear dragging state in `MoveSizeEnd()`**, even when the snapper is already null, or the
  state strands and steals keys (PR #48569).
- **Keep key-swallowing narrow.** In `OnKeyDown`, swallow only real snap/quick-layout hotkeys and the
  **bare** Shift during a drag; never `Shift+<other>`; gate digit layout-switch behind Win+Ctrl+Alt
  (PR #48569).
- **Subscribe to every WinEvent you dispatch.** A `WM_PRIV_*` branch is dead code unless the matching
  `EVENT_*` is registered in the WinEvent hook (PR #48569 added `EVENT_OBJECT_DESTROY`).
- **Resolve monitor/virtual-desktop ids defensively.** VD GUIDs come from the registry and vary by OS
  build; multi-monitor work-area ids drive zone-history lookup — mismatches route windows to the wrong monitor
  (#49057, #47010).
- **Serialize and error-handle the JSON data files.** `applied-layouts.json`, `app-zone-history.json`,
  custom/default layouts are shared on disk; unguarded writes cause access-denied (#48374).
- **Localize editor strings with translator context.** UI strings like "Space around zones" and
  "Highlight distance" need translator comments (PR #47226).
- **Add a test where a harness exists.** Native logic → `FancyZonesTests`; editor →
  `FancyZonesEditor.UnitTests`; end-to-end → the `*.UITests`. FancyZones has **no** unit-test harness
  for the live hook/drag path — call that out and validate manually (PR #48569).

## Pitfalls

- **Never** call `thread::join()` in a destructor without `joinable()` — a ctor early-return (display
  TDR, monitor disconnect) leaves the render thread non-joinable → `std::terminate` (PR #48473).
- **Never** return an overlay HWND to the window pool before its render thread is stopped — the pool
  recycles it and two render targets draw into one window (PR #48473).
- **Never** signal a condition-variable shutdown flag outside the waiter's mutex — the wakeup can be
  missed and `join()` hangs (PR #48473).
- **Never** keep a raw `WorkArea*` alive across `WorkAreaConfiguration::Clear()` — monitor changes and
  the span-across toggle free it mid-drag (PR #48473).
- **Never** call `MoveSizeEnd()` for a window that was destroyed mid-drag — it snaps a dead HWND;
  use `Abort()` (PR #48569).
- **Stuck `dragging` state steals number keys** — if drag state strands true, any digit switches
  layouts; always clear it and require Win+Ctrl+Alt (PR #48569).
- **A `WM_PRIV_*` handler is dead** unless the matching `EVENT_*` is registered in the WinEvent hook.
- **Virtual-desktop GUIDs live in the registry and move across OS builds** — never assume a fixed key
  path; per-VD layout binding breaks silently otherwise (#49057).
- **Zone history is keyed per app + work-area + layout**, not per window — multiple windows of one
  process can collapse into one zone (#47010).
- **PowerShell eats `{GUID}` as a script block** — the CLI must accept brace-less GUIDs (#44676).

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**; then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you on recurring
themes and measurably lowers your catch rate on the PR's actual issues. If a symptom doesn't map to
a row, reason from the source, not the map. Best for planning / triage; a targeted checklist (not a
script) for review.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + open reports.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a FancyZones PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/fancyzones/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/fancyzones)
- [WinEvent hooks (SetWinEventHook)](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook) · [std::thread::joinable](https://en.cppreference.com/w/cpp/thread/thread/joinable) · [condition_variable notify/wait](https://en.cppreference.com/w/cpp/thread/condition_variable)
