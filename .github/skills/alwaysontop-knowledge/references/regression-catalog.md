# AlwaysOnTop Evidence & Decision Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split:** `SKILL.md` owns the actionable symptom → cause → guardrail playbooks. This catalog
> retains provenance, source coordinates, chronology, maintainer decisions, unresolved clusters,
> and evidence caveats without restating those playbooks.

## Decision chronology

Ordered by the referenced PR sequence; no merge dates are asserted here.

| Artifact | Source coordinates | Recorded decision / review outcome |
|---|---|---|
| [PR #44815](https://github.com/microsoft/PowerToys/pull/44815) | `AlwaysOnTop.cpp` `RegisterLLKH`, `ApplyWindowAlpha`, `RestoreWindowAlpha`, `ResolveTransparencyTargetWindow` | Preserve the original layered-window state, use one target HWND for apply/restore, retain recovery state after failed restoration, validate critical event handles, and check state-changing Win32 calls. |
| [PR #45773](https://github.com/microsoft/PowerToys/pull/45773) | `AlwaysOnTop.cpp` `UpdateSystemMenuItem`, `UpdateSystemMenuEventHooks`, `HandleWinHookEvent`; persisted `ShowInSystemMenu` setting and Settings UI serialization | Chose a system-menu entry rather than a title-bar button; kept the feature opt-in (`showInSystemMenu = false`); review required global menu hooks to exist only while enabled and requested persisted-setting coverage. |
| [PR #45845](https://github.com/microsoft/PowerToys/pull/45845) | `AlwaysOnTop.cpp` `UpdateSystemMenuItem`, `IsAlwaysOnTopMenuCommand` | Adopted owner tagging with `dwItemData = 0x414F5450`; a same-ID foreign item is not updated or removed. |
| [issue #45993](https://github.com/microsoft/PowerToys/issues/45993), [PR #45994](https://github.com/microsoft/PowerToys/pull/45994) | `Settings.cpp` `LoadSettings`, `InitFileWatcher`, `NotifyObservers`; `Settings.h` `AlwaysOnTopSettings::settings()` | Published immutable settings snapshots through `std::atomic<std::shared_ptr<const Settings>>`; review required one snapshot load per operation. The PR discussion records synchronous IPC acknowledgements as a stronger but intentionally unchosen consistency model. |
| [PR #46410](https://github.com/microsoft/PowerToys/pull/46410) | `AlwaysOnTopModuleInterface/dllmain.cpp` `get_hotkeys`, `parse_hotkey`; `Settings` opacity-hotkey fields | Split increase/decrease opacity into independently configurable shortcuts; review retained the `min(buffer_size, count)` fill limit and `isShown = (key != 0)` contract. |
| [PR #46910](https://github.com/microsoft/PowerToys/pull/46910) | `AlwaysOnTop.cpp` `ProcessCommand` | Pin/unpin sound became success feedback and is gated on an actual state change. The evidence set records no replacement feedback when sound is disabled. |
| [PR #48412](https://github.com/microsoft/PowerToys/pull/48412) | `WindowBorder.cpp` `WindowBorder::UpdateBorderPosition` | Added lifetime/null-state validation around `m_trackingWindow`, `m_frameDrawer`, and `m_window` during timer refresh. |

## Issue and symptom-cluster ledger

| Cluster | Evidence | Exact source locations | Ledger status |
|---|---|---|---|
| Foreign/custom system-menu compatibility | [#46483](https://github.com/microsoft/PowerToys/issues/46483), [#46569](https://github.com/microsoft/PowerToys/issues/46569), [#46804](https://github.com/microsoft/PowerToys/issues/46804), [#46808](https://github.com/microsoft/PowerToys/issues/46808), [#47058](https://github.com/microsoft/PowerToys/issues/47058), [#47247](https://github.com/microsoft/PowerToys/issues/47247), [#47917](https://github.com/microsoft/PowerToys/issues/47917), [#48006](https://github.com/microsoft/PowerToys/issues/48006), [PR #45773](https://github.com/microsoft/PowerToys/pull/45773) | `AlwaysOnTop.cpp` `UpdateSystemMenuItem`, `HandleWinHookEvent`, `SubscribeToEvents`, `UpdateSystemMenuEventHooks` | Multi-application report cluster remains the primary compatibility evidence; this ledger does not establish that every application-specific report is resolved. |
| System-menu command ownership / duplication | [PR #45845](https://github.com/microsoft/PowerToys/pull/45845) | `AlwaysOnTop.cpp` `UpdateSystemMenuItem`, `IsAlwaysOnTopMenuCommand`; command `0xEFE0`, owner tag `0x414F5450` | Implemented decision; foreign same-ID behavior should be checked against the current source. |
| Live settings publication | [#45993](https://github.com/microsoft/PowerToys/issues/45993), [PR #45994](https://github.com/microsoft/PowerToys/pull/45994) | `Settings.cpp` `LoadSettings`, `InitFileWatcher`; `Settings.h` `settings()` | Fix evidence is a merged PR plus source inspection; full cross-process strong consistency was explicitly out of scope. |
| Opacity shortcut layouts and keypad variants | [#46135](https://github.com/microsoft/PowerToys/issues/46135), [#46209](https://github.com/microsoft/PowerToys/issues/46209), [#46300](https://github.com/microsoft/PowerToys/issues/46300), [#46387](https://github.com/microsoft/PowerToys/issues/46387), [#46391](https://github.com/microsoft/PowerToys/issues/46391), [PR #46410](https://github.com/microsoft/PowerToys/pull/46410) | `AlwaysOnTopModuleInterface/dllmain.cpp` `get_hotkeys`, `parse_hotkey`; `Settings` `increaseOpacityHotkey`, `decreaseOpacityHotkey` | Configurability shipped; the issue set is still useful coverage evidence for localized layouts and distinct numpad virtual keys. |
| Transparency restoration | [PR #44815](https://github.com/microsoft/PowerToys/pull/44815) review thread | `AlwaysOnTop.cpp` `ApplyWindowAlpha`, `RestoreWindowAlpha`, `ResolveTransparencyTargetWindow`; `m_windowOriginalLayeredState` | Reviewer-derived constraints; verify the final merged implementation because several details originated in review discussion. |
| Elevated-window boundary | [#46775](https://github.com/microsoft/PowerToys/issues/46775), [#47549](https://github.com/microsoft/PowerToys/issues/47549) | `AlwaysOnTop.cpp` pin path (`SetWindowPos`, `SetProp`), WinEvent hooks, border assignment | Platform/UIPI limitation cluster; no in-process non-elevated fix is evidenced here. |
| Cross-language default drift | [#46961](https://github.com/microsoft/PowerToys/issues/46961) | `AlwaysOnTop/Settings.h` default `RGB(0,173,239)` (`#00ADEF`); `Settings.UI.Library/AlwaysOnTopProperties.cs` default `#0099cc` | Known current violation verified in source; visible when the accent-color option is off. |
| `wstring_view` conversion safety | [#46962](https://github.com/microsoft/PowerToys/issues/46962) | `Settings.cpp` `HexToRGB`, specifically `std::stoll(hex.data())` | Known current violation verified in source: `wstring_view::data()` is not guaranteed null-terminated. |
| Border refresh lifetime | [PR #48412](https://github.com/microsoft/PowerToys/pull/48412) | `WindowBorder.cpp` `UpdateBorderPosition`; 100 ms `WM_TIMER` path | Fix PR evidence. |
| LLKH event-handle reliability | [PR #44815](https://github.com/microsoft/PowerToys/pull/44815) review thread | `AlwaysOnTop.cpp` `RegisterLLKH`; four-handle `MsgWaitForMultipleObjects` wait | Reviewer-derived risk; confirm whether all proposed validation landed. |
| Ignored Win32 failures | [PR #44815](https://github.com/microsoft/PowerToys/pull/44815) review thread | `RegisterHotkey`, `PinTopmostWindow`, `ApplyWindowAlpha`, `RestoreWindowAlpha` | Reviewer decision, not proof that every call site now checks every return value. |

## Stable source facts used by the ledger

- Border implementation: `WindowBorder.cpp` creates a per-pinned-window layered, topmost
  `WS_EX_TOOLWINDOW`; its timer path refreshes every 100 ms and reads
  `DWMWA_EXTENDED_FRAME_BOUNDS`.
- DWM/DPI coordinates: `WindowBorder.cpp` `GetFrameRect`,
  `ScalingUtils.cpp` `ScalingFactor`, and `WindowCornersUtil.cpp` `CornersRadius`.
- C++/C# parity coordinates: `AlwaysOnTop/Settings.h` and
  `Settings.UI.Library/AlwaysOnTopProperties.cs`.
- Build-review carry-over: [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) records the
  repo-wide `$(RepoRoot)` path convention and sensitive `Microsoft.Cpp.*.props` import ordering.

## Evidence-quality notes

- The original collection reported **12 merged PRs, 84 review comments, and 30 bug issues**, with
  source verification under `src/modules/alwaysontop`; those counts are retained as collection
  metadata, not independently reproduced here.
- Issue reports establish observed symptoms, not causality or present-day reproducibility.
- Review comments record maintainer intent but may describe requested changes that were amended or
  not included in the final merge; inspect the final diff and current source before enforcement.
- Excluded from module conclusions: CI commands, approvals, formatting chatter, and cross-cutting
  build changes except the durable `$(RepoRoot)`/import-order decision above.
