# FancyZones Regression Catalog (Progressive Disclosure)

Fuller regression + decision list. Read the row for the area your change touches; confirm each
claim in source before acting. Symptoms map to `src/modules/fancyzones/`.

## Key Decisions (context for the playbooks)

- **Host process is hook-driven; work is posted to its own window.** WinEvent-hook callbacks post
  `WM_PRIV_*` messages to the FancyZones tool window; `WndProc`/`HandleWinHookEvent` dispatch them.
  A `WM_PRIV_*` branch is dead unless the matching `EVENT_*` is registered (`FancyZones.cpp`).
- **Each monitor owns a `WorkArea`, which owns a `Layout` + a `ZonesOverlay`.** Overlays render on a
  dedicated D2D thread (`ZonesOverlay::RenderLoop`) and their HWNDs come from a reusable pool
  (`FreeZonesOverlayWindow`/`NewZonesOverlayWindow`). Teardown order is safety-critical (PR #48473).
- **Monitor state changes rebuild the work-area map.** `FancyZones::UpdateWorkAreas` +
  `WorkAreaConfiguration::Clear()` run on display changes and the `SpanZonesAcrossMonitors` toggle;
  anything holding a `WorkArea*` across this must be torn down first (PR #48473).
- **Drag state is explicit and must never strand.** `DraggingState` tracks the active flag / Shift /
  Ctrl; `MoveSizeEnd()` always disables it. A stuck flag steals number keys via quick-layout
  switching (PR #48569).
- **Layouts, hotkeys, and per-app zone history persist as JSON** under `FancyZonesLib/FancyZonesData/`
  (`AppliedLayouts`, `CustomLayouts`, `DefaultLayouts`, `LayoutTemplates`, `LayoutHotkeys`,
  `AppZoneHistory`). These are shared on disk — writes need error handling (#48374).
- **Virtual-desktop ids are read from the registry** (`VirtualDesktop.cpp`) and synced into zone
  history (`AppZoneHistory::SyncVirtualDesktops`); registry layout varies by OS build (#49057).
- **No unit-test harness for the live hook/drag path.** Native logic has `FancyZonesTests`, the editor
  has `FancyZonesEditor.UnitTests`, and there are `*.UITests`, but drag/hook fixes are validated
  manually (PR #48569).

## Regression Table

| Class | Symptom | Where (file · function) | Root cause | Fix / Guardrail | Evidence |
|---|---|---|---|---|---|
| Teardown race | Host crash/hang on display/monitor change or exit | `ZonesOverlay.cpp` dtor; `WorkArea.cpp::~WorkArea`; `OnThreadExecutor.cpp` dtor | join on non-joinable thread; HWND freed before render stops; shutdown flag set outside mutex | `joinable()` guard; reset overlay before pool free; write flag under `_task_mutex` | [PR #48473](https://github.com/microsoft/PowerToys/pull/48473) |
| Teardown race | Crash toggling span-across / monitor change mid-drag | `FancyZones.cpp::UpdateWorkAreas`; `WorkAreaConfiguration::Clear` | `WindowMouseSnap` holds dangling `WorkArea*`/`const&` across `Clear()` | Call `MoveSizeEnd()` before every `Clear()` | [PR #48473](https://github.com/microsoft/PowerToys/pull/48473) |
| Stuck drag | Overlays stay up + number/Shift keys swallowed after closing window mid-drag | `FancyZones.cpp` `EVENT_OBJECT_DESTROY` / `WM_PRIV_WINDOWDESTROYED` / `OnKeyDown`; `WindowMouseSnap::Abort` | destroy event never subscribed → drag state strands; any digit switched layouts | subscribe `EVENT_OBJECT_DESTROY`; `Abort()` on dragged-window destroy; always clear drag in `MoveSizeEnd()`; require Win+Ctrl+Alt+digit; swallow only bare Shift | [PR #48569](https://github.com/microsoft/PowerToys/pull/48569) |
| Override snap | Win+arrow snaps native first / extra hotkeys / no override | `FancyZones.cpp::ShouldProcessSnapHotkey` + `OnKeyDown`; `Settings.overrideSnapHotkeys`; `WindowKeyboardSnap.cpp` | swallow decision vs. monitor topology + move-by-position setting | keep swallow decision consistent with snap; honor setting; confirm candidate | (open) [#47580](https://github.com/microsoft/PowerToys/issues/47580), [#48387](https://github.com/microsoft/PowerToys/issues/48387), [#48048](https://github.com/microsoft/PowerToys/issues/48048) |
| Last known zone | All app windows collapse into one zone; blank/black on multi-monitor | `AppZoneHistory.cpp::GetAppLastZoneIndexSet`; new-window handling in `FancyZones.cpp` | history keyed per app+work-area+layout, not per window; work-area id misroute | match work-area/layout id to target monitor; beware per-process keys | (open) [#47010](https://github.com/microsoft/PowerToys/issues/47010), [#48234](https://github.com/microsoft/PowerToys/issues/48234), [#49209](https://github.com/microsoft/PowerToys/issues/49209) |
| VD / JSON | Per-desktop layouts stop working; `applied-layouts.json` access denied | `AppliedLayouts.cpp`; `VirtualDesktop.cpp`; `AppZoneHistory::SyncVirtualDesktops` | VD GUIDs move across OS builds; unguarded/locked JSON write | resolve VD id defensively; serialize + error-handle writes; sync ids on desktop change | (open) [#49057](https://github.com/microsoft/PowerToys/issues/49057), [#48374](https://github.com/microsoft/PowerToys/issues/48374) |
| Shift drag | Hold-Shift-to-activate disables Shift while typing / doesn't stick | `FancyZones.cpp::OnKeyDown`; `DraggingState.cpp` | over-broad Shift swallow / drag state timing | swallow only bare Shift during an active drag | (open) [#47823](https://github.com/microsoft/PowerToys/issues/47823), [#47780](https://github.com/microsoft/PowerToys/issues/47780), [#48641](https://github.com/microsoft/PowerToys/issues/48641) |
| Editor | Ctrl+Tab shortcut misbehaves; spacing/highlight labels inaccurate | `editor/FancyZonesEditor/` view-models, models | UX + localization gaps | translator comments; match native layout math | [PR #47226](https://github.com/microsoft/PowerToys/pull/47226); (open) [#48315](https://github.com/microsoft/PowerToys/issues/48315), [#47959](https://github.com/microsoft/PowerToys/issues/47959) |
| CLI | `{GUID}` fails in PowerShell | `FancyZonesCLI/CommandLine/Commands/` | PowerShell parses `{...}` as script block | accept brace-less GUID; friendly error; subcommand `--help` | [PR #44676](https://github.com/microsoft/PowerToys/pull/44676) (#44633, #44675) |

## Common Practices (enforced in review)

- **Race-safe teardown.** Guard `join()` with `joinable()`; stop render threads before recycling
  HWNDs; pair `cv` shutdown-flag writes with the waiter's mutex; tear down the snapper before
  clearing the work-area map (PR #48473).
- **Narrow key-swallowing.** `OnKeyDown` returns true only for genuine snap/quick-layout hotkeys and
  the bare Shift during a drag; digit layout switch needs Win+Ctrl+Alt (PR #48569).
- **Subscribe before you dispatch.** Register the `EVENT_*` for any `WM_PRIV_*` you handle.
- **Defensive monitor/VD id resolution.** Registry-sourced VD ids and multi-monitor work-area ids
  drive layout binding and zone-history lookup; mismatches misroute windows silently (#49057, #47010).
- **Guarded JSON persistence.** Shared data files under `FancyZonesLib/FancyZonesData/` must serialize and
  error-handle writes (#48374).
- **Localize editor strings with translator context** (PR #47226).
- **Testing.** Use `FancyZonesTests` / `FancyZonesEditor.UnitTests` / `*.UITests` where a harness
  exists; document manual steps for the hook/drag path (PR #48569).

## Excluded as noise (not durable FancyZones lessons)

Build/infra PRs in the mined corpus that carry no module-specific engineering lesson:
`.NET 10` upgrade (#41280), VS 2026 support (#44304), CppWinRT bump (#45420), WinRT coroutine
refactor (#45522), `$(RepoRoot)` path cleanup (#44639), MTP migration (#37651), PowerShell build-script
reliability (#46729), and UI-test fixups (#44754). These are repo-wide plumbing, not FancyZones
behavior.

---
*Corpus: 12 merged PRs, ~100 review comments (mostly automated), 30 bug issues + source verification
against `src/modules/fancyzones`. FancyZones-specific signal concentrated in PR #48473, #48569,
#47226, #44676; the rest is build/infra noise (see above).*
