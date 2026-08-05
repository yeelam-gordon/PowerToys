# FancyZones Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

## Role split

`SKILL.md` owns current symptom, root-cause, and guardrail guidance. This catalog preserves the
historical evidence trail, source anchors, reviewer decisions, unresolved reports, and caveats used
to audit or refresh that guidance. Confirm source anchors before relying on them.

## Evidence ledger

| Sequence | Evidence | Source anchors | Recorded outcome / reviewer decision |
|---|---|---|---|
| Teardown investigation and fix | [PR #48473](https://github.com/microsoft/PowerToys/pull/48473) | `ZonesOverlay.cpp::~ZonesOverlay`; `WorkArea.cpp::~WorkArea`; `OnThreadExecutor.cpp` destructor/worker; `FancyZones.cpp::UpdateWorkAreas`; `WorkAreaConfiguration::Clear` | Accepted changes guarded thread joins, stopped overlays before returning HWNDs to the pool, synchronized the shutdown flag with `_task_mutex`, and ended an active snap before clearing work areas. |
| Destroyed-window drag fix | [PR #48569](https://github.com/microsoft/PowerToys/pull/48569) | `FancyZones.cpp::HandleWinHookEvent`, `WM_PRIV_WINDOWDESTROYED`, `MoveSizeEnd`, `OnKeyDown`; `WindowMouseSnap::Abort`; `DraggingState` | Accepted changes registered `EVENT_OBJECT_DESTROY`, used `Abort()` for the destroyed dragged HWND, always cleared drag state, required Win+Ctrl+Alt for digit layout switching, and limited Shift swallowing to bare Shift during drag. |
| Live hook validation decision | [PR #48569](https://github.com/microsoft/PowerToys/pull/48569) | `FancyZonesTests`, `FancyZonesEditor.UnitTests`, `*.UITests` | Review recorded no unit-test harness for the live hook/drag path; that portion was validated manually while existing harnesses remained applicable elsewhere. |
| Editor review | [PR #47226](https://github.com/microsoft/PowerToys/pull/47226) | `editor/FancyZonesEditor/` view-models and models | Reviewer outcome retained translator context and alignment between editor labels/math and native layout behavior. |
| CLI chronology | Issues [#44633](https://github.com/microsoft/PowerToys/issues/44633), [#44675](https://github.com/microsoft/PowerToys/issues/44675) → [PR #44676](https://github.com/microsoft/PowerToys/pull/44676) | `FancyZonesCLI/CommandLine/Commands/` | The CLI accepted brace-less GUIDs, added a PowerShell-aware error path, and exposed per-subcommand help. |
| Hook architecture | Source verification | `FancyZones.cpp::HandleWinHookEvent`, `WndProc`, `OnKeyDown` | WinEvent callbacks post `WM_PRIV_*` messages to the tool window. A private-message branch has no effect unless its matching `EVENT_*` is registered. |
| Overlay/work-area ownership | Source verification plus [PR #48473](https://github.com/microsoft/PowerToys/pull/48473) | `WorkArea.cpp`; `ZonesOverlay.cpp::RenderLoop`; `NewZonesOverlayWindow`, `FreeZonesOverlayWindow` | Each monitor work area owns its layout/overlay; pooled HWND lifetime is ordered after render-thread teardown. |
| Persistence architecture | Source verification | `FancyZonesLib/FancyZonesData/AppliedLayouts.cpp`, `CustomLayouts.cpp`, `DefaultLayouts.cpp`, `LayoutTemplates.cpp`, `LayoutHotkeys.cpp`, `AppZoneHistory.cpp` | Layout, hotkey, and app-zone-history records are shared JSON state; access and migration assumptions require source confirmation. |
| Virtual desktop binding | Source verification | `VirtualDesktop.cpp::GetCurrentVirtualDesktopIdFromRegistry`, `GetVirtualDesktopIdsFromRegistry`; `AppZoneHistory::SyncVirtualDesktops` | Virtual-desktop identifiers are registry-derived and synchronized into history rather than treated as fixed constants. |

## Decision ledger

| Decision | Status | Evidence / anchor |
|---|---|---|
| Guard every optional render thread before joining. | Accepted | [PR #48473](https://github.com/microsoft/PowerToys/pull/48473); `ZonesOverlay.cpp` |
| Tear down an overlay before recycling its HWND. | Accepted | [PR #48473](https://github.com/microsoft/PowerToys/pull/48473); `WorkArea.cpp::~WorkArea` |
| Change the executor shutdown flag under the waiter's mutex. | Accepted | [PR #48473](https://github.com/microsoft/PowerToys/pull/48473); `OnThreadExecutor.cpp` |
| End active mouse snapping before `WorkAreaConfiguration::Clear()`. | Accepted | [PR #48473](https://github.com/microsoft/PowerToys/pull/48473); `FancyZones.cpp::UpdateWorkAreas` |
| Abort rather than complete a drag after its HWND is destroyed. | Accepted | [PR #48569](https://github.com/microsoft/PowerToys/pull/48569); `WindowMouseSnap::Abort` |
| Keep key swallowing restricted to confirmed snap/layout commands and bare Shift during drag. | Accepted | [PR #48569](https://github.com/microsoft/PowerToys/pull/48569); `FancyZones.cpp::OnKeyDown` |
| Resolve monitor and virtual-desktop identity defensively before history/layout lookup. | Accepted; open reports remain | `MonitorUtils.cpp`; `VirtualDesktop.cpp`; `AppZoneHistory.cpp` |
| Applied-layout writes have an access-denied report; concurrency versus permissions is unresolved. | Open evidence, not an accepted fix | `AppliedLayouts.cpp`; [#48374](https://github.com/microsoft/PowerToys/issues/48374) |

## Open-issue ledger

| Area | Open evidence | Source anchors | Evidence caveat / unresolved question |
|---|---|---|---|
| Override Windows Snap | [#47580](https://github.com/microsoft/PowerToys/issues/47580), [#48387](https://github.com/microsoft/PowerToys/issues/48387), [#48048](https://github.com/microsoft/PowerToys/issues/48048) | `FancyZones.cpp::ShouldProcessSnapHotkey`, `OnKeyDown`; `Settings.overrideSnapHotkeys`; `WindowKeyboardSnap.cpp` | Reports cover different topology/settings combinations; they do not establish one shared defect without reproduction. |
| Last-known-zone restore | [#47010](https://github.com/microsoft/PowerToys/issues/47010), [#48234](https://github.com/microsoft/PowerToys/issues/48234), [#49209](https://github.com/microsoft/PowerToys/issues/49209) | `AppZoneHistory.cpp::GetAppLastZoneIndexSet`; new-window handling in `FancyZones.cpp` | History is keyed by app, work area, and layout rather than HWND. Confirm target-monitor identity and app-specific behavior before changing the key model. |
| Virtual desktops / JSON | [#49057](https://github.com/microsoft/PowerToys/issues/49057), [#48374](https://github.com/microsoft/PowerToys/issues/48374) | `AppliedLayouts.cpp`; `VirtualDesktop.cpp`; `AppZoneHistory::SyncVirtualDesktops` | One report concerns OS-dependent desktop identifiers and one concerns file access; do not collapse them into a single cause. |
| Shift behavior | [#47823](https://github.com/microsoft/PowerToys/issues/47823), [#47780](https://github.com/microsoft/PowerToys/issues/47780), [#48641](https://github.com/microsoft/PowerToys/issues/48641) | `FancyZones.cpp::OnKeyDown`; `DraggingState.cpp` | Reports span key swallowing and activation timing. Reproduce against current drag state before revising the accepted PR #48569 behavior. |
| Editor behavior | [#48315](https://github.com/microsoft/PowerToys/issues/48315), [#47959](https://github.com/microsoft/PowerToys/issues/47959) | `editor/FancyZonesEditor/` | UX reports require comparison with native layout math and localized strings; PR #47226 is prior review context, not proof of the open reports' causes. |

## Corpus caveats

- The durable FancyZones signal was concentrated in PRs
  [#48473](https://github.com/microsoft/PowerToys/pull/48473),
  [#48569](https://github.com/microsoft/PowerToys/pull/48569),
  [#47226](https://github.com/microsoft/PowerToys/pull/47226), and
  [#44676](https://github.com/microsoft/PowerToys/pull/44676).
- Repo-wide plumbing was excluded: .NET 10 #41280, VS 2026 #44304, CppWinRT #45420, WinRT
  coroutine refactor #45522, `$(RepoRoot)` cleanup #44639, MTP migration #37651, PowerShell
  build-script reliability #46729, and UI-test fixups #44754.
- Automated review comments dominated the mined review corpus; source verification is required
  before treating a comment as an accepted maintainer decision.

---
*Corpus: 12 merged PRs, approximately 100 review comments, 30 bug issues, plus source verification
against `src/modules/fancyzones`.*
