# ZoomIt — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split note:** `SKILL.md` owns the operational symptom → root-cause → guardrail playbooks. This
> reference keeps only source anchors, evidence chronology, reviewer decisions, unresolved clusters,
> and evidence limits.

## Evidence caveats

- Confirm symbols against the current branch. `Zoomit.cpp` is a large global-state message pump, so
  line numbers drift; this ledger uses function/message-case anchors instead.
- Most issue bodies in the mined corpus were title-level. Issue clusters identify observed surface
  area, not a verified common mechanism.
- Merged PRs and explicit review comments are decision evidence. Source-only rows describe current
  implementation, not necessarily a maintainer-approved invariant.

## Source-anchor ledger

| Area | Exact source anchors | Evidence retained | Decision or current fact | Basis |
|---|---|---|---|---|
| Message pump and shared state | `src/modules/ZoomIt/ZoomIt/Zoomit.cpp::MainWndProc`, `WM_HOTKEY`; file-scope `g_*` state | — | ZoomIt remains a single global-state message-pump application; cross-mode effects must be traced through `MainWndProc`. | Source |
| XOR-derived hotkeys | `Zoomit.cpp::RegisterAllHotkeys`; `OptionsProc` hotkey validation; `MainWndProc` `WM_CREATE`; `MainWndProc` `WM_USER_RELOAD_SETTINGS`; IDs `RECORD_CROP_HOTKEY`, `RECORD_WINDOW_HOTKEY`, `LIVE_DRAW_HOTKEY`, `DEMOTYPE_RESET_HOTKEY` | [PR #47388](https://github.com/microsoft/PowerToys/pull/47388), [PR #48266](https://github.com/microsoft/PowerToys/pull/48266), [PR #48401](https://github.com/microsoft/PowerToys/pull/48401) | Accepted decision: compute named derived modifiers, reject zero modifiers, and keep all four registration/validation sites behaviorally aligned. #48266/#48401 retain the standalone regression/follow-up history. | Merged fixes + review |
| Hotkey registration helper | `Zoomit.cpp::RegisterAllHotkeys` local `registerHotkey` lambda | [PR #49075](https://github.com/microsoft/PowerToys/pull/49075) | Review required the logging helper instead of new raw `RegisterHotKey` calls. | Reviewer decision |
| Toggle/save independence | `Zoomit.cpp::OptionsProc`; `MainWndProc` `WM_CREATE`; `MainWndProc` `WM_USER_RELOAD_SETTINGS`; IDs `SNIP_HOTKEY`, `SNIP_SAVE_HOTKEY`, `SNIP_PANORAMA_HOTKEY`, `SNIP_PANORAMA_SAVE_HOTKEY` | [PR #49075](https://github.com/microsoft/PowerToys/pull/49075), [#46938](https://github.com/microsoft/PowerToys/issues/46938) | Review separated toggle and save validation/registration and excluded zero virtual keys from registration. | Merged fix + issue signal |
| International keyboard interaction | `Zoomit.cpp::GetKeyMod`; `MainWndProc` `WM_HOTKEY`; default hotkey initialization | [#48377](https://github.com/microsoft/PowerToys/issues/48377), [#47491](https://github.com/microsoft/PowerToys/issues/47491), [#46656](https://github.com/microsoft/PowerToys/issues/46656), [#47836](https://github.com/microsoft/PowerToys/issues/47836), [#47072](https://github.com/microsoft/PowerToys/issues/47072) | AltGr surfaces as Ctrl+Alt to this path. The cluster is retained as layout-compatibility evidence; no single fix decision is established here. | Source + issue signal |
| Recording orchestration | `Zoomit.cpp::StartRecordingAsync`; `VideoRecordingSession::Create`; `GifRecordingSession::Create`; `AudioSampleGenerator::InitializeAsync`; shared `g_RecordingSession`, `g_GifRecordingSession` teardown | [PR #48685](https://github.com/microsoft/PowerToys/pull/48685), [#48368](https://github.com/microsoft/PowerToys/issues/48368), [#47877](https://github.com/microsoft/PowerToys/issues/47877), [#47773](https://github.com/microsoft/PowerToys/issues/47773), [#47316](https://github.com/microsoft/PowerToys/issues/47316), [#46006](https://github.com/microsoft/PowerToys/issues/46006) | #48685 accepted early audio initialization for latency but required joining it before use. Session creation/close/null ordering remains explicit for MP4 and GIF state. | Merged fix + source + issue signal |
| Recording filename policy | `Zoomit.cpp::GetUniqueRecordingFilename`, `::IsDefaultRecordingFilename`, `::GetTimestampSuffix`, `::GetUniqueScreenshotFilename` | [PR #43236](https://github.com/microsoft/PowerToys/pull/43236), [#43202](https://github.com/microsoft/PowerToys/issues/43202) | Accepted policy: timestamp the default recording name; preserve custom-name digits and add `(n)` only on collision. Screenshot naming remains timestamp-based. | Merged fix |
| Live Zoom and LiveDraw | runtime pointers `pMagSetWindowSource`, `pMagSetWindowTransform`; `g_hWndLiveZoom`, `g_hWndLiveZoomMag`; `LIVE_DRAW_HOTKEY`; layered-window creation and `g_LiveZoomLevel` pen scaling | [#46369](https://github.com/microsoft/PowerToys/issues/46369) | Magnification entry points are runtime-loaded; LiveDraw is a layered annotation path over Live Zoom. Pen/touch behavior remains issue evidence, not a closed decision. | Source + issue signal |
| Draw/highlight | `Zoomit.cpp::DrawHighlightedShape` | [#47329](https://github.com/microsoft/PowerToys/issues/47329) | The issue records a compositing artifact; this catalog does not infer a verified cause. | Issue signal |
| DPI, cursor, and webcam overlay | `Utility.cpp::GetDpiForWindowHelper`, `::ScaleDialogForDpi`, `::ScaleForDpi` (declared in `Utility.h`); `Zoomit.cpp::MainWndProc` `WM_DPICHANGED`; `MonitorFromPoint`/`GetMonitorInfo` target selection; `WebcamCapture.cpp`; `WebcamPreviewWindow.cpp`; `WebcamComposite.hlsl` | [#48823](https://github.com/microsoft/PowerToys/issues/48823), [#47736](https://github.com/microsoft/PowerToys/issues/47736), [#48508](https://github.com/microsoft/PowerToys/issues/48508), [#48529](https://github.com/microsoft/PowerToys/issues/48529), [#48857](https://github.com/microsoft/PowerToys/issues/48857), [#48367](https://github.com/microsoft/PowerToys/issues/48367), [#48188](https://github.com/microsoft/PowerToys/issues/48188) | Options controls participate in manual DPI scaling; monitor targeting is point-based. The issue set spans cursor restoration, secondary-display behavior, webcam geometry, duplicate taskbar UI, and dialog scrolling without proving one cause. | Source + issue signal |
| Panorama conflict path | `ZoomIt/PanoramaCapture.cpp`; `MainWndProc` panorama `WM_HOTKEY` guard | [#47154](https://github.com/microsoft/PowerToys/issues/47154) → [PR #47215](https://github.com/microsoft/PowerToys/pull/47215) | Closed completed by replacing the blocking MessageBox path in PowerToys-hosted ZoomIt. | Merged fix |
| External capture interoperability | ZoomIt overlay window creation/styles and capture-affinity handling | [#48850](https://github.com/microsoft/PowerToys/issues/48850) | Whether external capture should include annotations remains unresolved; no implementation decision is recorded. | Issue signal |
| Settings round-trip | `ZoomItSettings.h` `RegSettings[]`; `Registry.h`; `ZoomItSettingsInterop/ZoomItSettings.cpp`; `Settings.UI/ViewModels/ZoomItViewModel.cs`; `MainWndProc` `WM_USER_RELOAD_SETTINGS` | [PR #47539](https://github.com/microsoft/PowerToys/pull/47539) | Review distinguished UI label suppression in `ZoomItViewModel.cs` from native registration behavior; comments must not claim the converter changes registration. | Reviewer decision |
| Localized UI strings | settings `Resources.resw`; `ZoomItPage.xaml` | [PR #47529](https://github.com/microsoft/PowerToys/pull/47529), [PR #47539](https://github.com/microsoft/PowerToys/pull/47539) | Review required resource-backed end-user strings and Sentence casing. | Reviewer decision |

## Decision chronology

| Date | Artifact | Recorded decision |
|---|---|---|
| 2026-05-05 | [PR #47529](https://github.com/microsoft/PowerToys/pull/47529) merged | Webcam/append work carried the localization and Sentence-casing review decisions. |
| 2026-05-13 | [PR #47388](https://github.com/microsoft/PowerToys/pull/47388) merged | Added the non-zero XOR-derived modifier rule across duplicated hotkey sites. |
| 2026-06-09 | [PR #48401](https://github.com/microsoft/PowerToys/pull/48401) merged | Corrected a broken hotkey condition following the derived-hotkey changes. |
| 2026-06-11 | [PR #48266](https://github.com/microsoft/PowerToys/pull/48266) merged | Retained standalone hotkey regression history alongside webcam/noise-cancellation work. |
| 2026-06-16 | [PR #48685](https://github.com/microsoft/PowerToys/pull/48685) merged | Fixed the audio-initialization race while preserving early startup for latency. |
| 2026-06-18 | [PR #47539](https://github.com/microsoft/PowerToys/pull/47539) merged | Recorded wording/localization review and the UI-display-versus-registration distinction. |
| 2026-07-01 | [PR #43236](https://github.com/microsoft/PowerToys/pull/43236) merged | Established timestamp versus collision-suffix filename policy. |
| 2026-07-01 | [PR #49075](https://github.com/microsoft/PowerToys/pull/49075) merged | Separated toggle/save registration and required the logging registration helper. |

## Unresolved evidence clusters

| Cluster | Open evidence | What remains unresolved |
|---|---|---|
| AltGr and international layouts | [#48377](https://github.com/microsoft/PowerToys/issues/48377), [#47491](https://github.com/microsoft/PowerToys/issues/47491), [#46656](https://github.com/microsoft/PowerToys/issues/46656), [#47836](https://github.com/microsoft/PowerToys/issues/47836), [#47072](https://github.com/microsoft/PowerToys/issues/47072) | A layout-safe default/modifier policy across all ZoomIt variants. |
| Recording lifecycle | [#48368](https://github.com/microsoft/PowerToys/issues/48368), [#47877](https://github.com/microsoft/PowerToys/issues/47877), [#47773](https://github.com/microsoft/PowerToys/issues/47773), [#47316](https://github.com/microsoft/PowerToys/issues/47316), [#46006](https://github.com/microsoft/PowerToys/issues/46006) | Which MP4, GIF, trim/re-record, and audio failures remain after #48685. |
| Cursor, DPI, webcam, and multi-monitor UI | [#48823](https://github.com/microsoft/PowerToys/issues/48823), [#47736](https://github.com/microsoft/PowerToys/issues/47736), [#48508](https://github.com/microsoft/PowerToys/issues/48508), [#48529](https://github.com/microsoft/PowerToys/issues/48529), [#48857](https://github.com/microsoft/PowerToys/issues/48857), [#48367](https://github.com/microsoft/PowerToys/issues/48367), [#48188](https://github.com/microsoft/PowerToys/issues/48188) | Whether failures share coordinate conversion, cursor lifecycle, window ownership, or separate UI causes. |
| Drawing, touch, and external capture | [#47329](https://github.com/microsoft/PowerToys/issues/47329), [#46369](https://github.com/microsoft/PowerToys/issues/46369), [#48850](https://github.com/microsoft/PowerToys/issues/48850) | Compositing semantics, pen/touch coverage, and annotation capture policy remain unresolved; panorama conflict #47154 is resolved by PR #47215. |
| Editable save shortcut | [#46938](https://github.com/microsoft/PowerToys/issues/46938) (closed completed April 19, 2026) | Historical signal; PR #49075 is the merged toggle/save independence fix, so re-verify current settings surfaces before treating this as open. |

## Scope exclusions

CI commands, spell-check allowlists, pure typo comments, and dependency/toolchain churn were excluded
unless a review comment established a durable ZoomIt-specific decision.
