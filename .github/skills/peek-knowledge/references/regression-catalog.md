# Peek — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

This catalog is the progressive-disclosure evidence record for `SKILL.md`. Source anchors are under
`src/modules/peek/`.

> **Role split:** `SKILL.md` owns actionable symptom → root-cause → guardrail guidance. This file
> owns provenance: exact source anchors, historical decisions, reviewer rationale, unresolved issue
> clusters, chronology, and confidence caveats. Do not duplicate the playbook prose here.

## Verified source-anchor ledger

| Area | Exact source anchors | Evidence retained |
|---|---|---|
| Space activation/settings | `dllmain.cpp`: `m_enableSpaceToActivate`, `JSON_KEY_ENABLE_SPACE_TO_ACTIVATE`, `init_settings` | First-run Space mode and off/on restoration logic live runner-side. |
| Typing suppression | `Peek.UI/Helpers/FileExplorerHelper.cs::CaretVisible`, `GetSelectedItems` | Uses `GetGUIThreadInfo`, same top-level active-window check, `Edit`/`Input` class matching, then `GUI_CARETBLINKING`; failure returns “not typing.” |
| Empty selection | `Peek.UI/PeekXAML/MainWindow.xaml.cs::Initialize` | `ViewModel.CurrentItem == null` returns without showing or foregrounding Peek. |
| Hosted-content shortcuts | `MainWindow` `KeyboardAccelerator`s: `CloseInvoked`, `PreviousNavigationInvoked`, `NextNavigationInvoked`; hosted WebView2/Monaco and shell preview-handler window | Parent WinUI accelerators do not necessarily receive keys after hosted content owns focus. |
| Shell preview-handler COM | `ShellPreviewHandlerPreviewer.LoadPreviewAsync`, `Clear`, `ReleaseHandlerFactories`; `MainWindow.Uninitialize`, `AppWindow_Closing` | Cached factories are `LockServer(true)` local servers; teardown drains via `TryRemove`, mirrors `LockServer(false)`, and final-releases RCWs. |
| Media lifecycle | `VideoPreviewer`, `AudioPreviewer`: `Dispose`, `Unload`, `OnPreviewChanged` | `MediaSource` backs media playback and SMTC registration; source disposal is explicit. |
| Archive encoding | `ArchivePreviewer` zip path | Forced CP437 acts as a reversible probe; unknown encoding is detected with `UtfUnknown`; EFS/UTF-8-resolved keys remain as supplied by SharpCompress. |
| Formatting | `Peek.Common/Helpers/MathHelper.cs::NumberOfDigits`; `ReadableStringHelper.GetPrecision` | `Math.Abs(int.MinValue)` can throw; caller includes a `double`→`int` conversion. |
| Tooltip setting | `FilePreview.UpdateTooltipAsync`; `MainWindow.Initialize` | Async work rechecks the setting before assigning `InfoTooltip`; setting value is copied per initialization, not live-propagated. |

## Decision chronology

Ordered by the repository history represented in this corpus.

| Change | Evidence | Decision / reviewer record |
|---|---|---|
| Empty virtual-folder error UI introduced | [PR #44703](https://github.com/microsoft/PowerToys/pull/44703) | Added error/show/foreground behavior when no current item existed; this later proved to overlap with typing suppression. |
| Archive encoding fix | [#44790](https://github.com/microsoft/PowerToys/issues/44790), [PR #44799](https://github.com/microsoft/PowerToys/pull/44799) | Reviewer decision: when strict CP437 round-trip succeeds, do not default to UTF-8 on low detector confidence; UTF-8 is known wrong for those non-ASCII fallback bytes. |
| Empty-selection focus regression reverted | [PR #44995](https://github.com/microsoft/PowerToys/pull/44995) | Restored the silent return in `Initialize`; no `ShowError`, `Show`, or `BringToForeground` for empty/suppressed selection. |
| Tooltip async race/settings semantics | [PR #46624](https://github.com/microsoft/PowerToys/pull/46624) | Recheck `ShowFilePreviewTooltip` before final assignment. Maintainers rejected live propagation while Peek is open as inconsistent with other settings. |
| Media/SMTC teardown | [PR #46899](https://github.com/microsoft/PowerToys/pull/46899) | Dispose media sources when preview changes or Peek closes; do not rely on GC for player/session teardown. |
| Hosted-content shortcut hook fix | [PR #48293](https://github.com/microsoft/PowerToys/pull/48293) (merged July 9, 2026) | Landed with reviewer decisions: real validated module handle; log `Marshal.GetLastWin32Error()`; idempotent `KeyUp` subscription; marshal the hook structure and then filter `vkCode`; include LWin/RWin; consume only after successful `DispatcherQueue.TryEnqueue`; uninstall in `Dispose`. See [low-level hook documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc). |
| Preview-handler teardown hardening | [PR #48564](https://github.com/microsoft/PowerToys/pull/48564) | Drain cache with `TryRemove`, not snapshot+`Clear`; release per-load handler/stream/item ownership in `finally`; isolate `Unload` from final release; keep teardown best-effort; prevent exceptions escaping `AppWindow_Closing`. |

## Symptom-cluster ledger (lifecycle noted)

These reports retain symptom grouping and source-localization clues without asserting a shared root
cause.

| Cluster | Reports | Current evidence boundary |
|---|---|---|
| Space during rename/search | [#45133](https://github.com/microsoft/PowerToys/issues/45133), [#45137](https://github.com/microsoft/PowerToys/issues/45137), [#45145](https://github.com/microsoft/PowerToys/issues/45145), [#45383](https://github.com/microsoft/PowerToys/issues/45383), [#45642](https://github.com/microsoft/PowerToys/issues/45642), [#45667](https://github.com/microsoft/PowerToys/issues/45667), [#45886](https://github.com/microsoft/PowerToys/issues/45886) | Historical focus-steal cause is established for PR #44703/#44995. Current reports still require checking `CaretVisible`, active window/control class, and empty-selection UI behavior. |
| Disable/re-enable regression | [#49013](https://github.com/microsoft/PowerToys/issues/49013) | Report follows module off/on transitions; investigate `enable`/`disable`, hook installation, and eligibility recomputation. Do not classify it as a Space-setting or hotkey-revert defect without reproduction. |
| IME composition | [#45346](https://github.com/microsoft/PowerToys/issues/45346), [#48189](https://github.com/microsoft/PowerToys/issues/48189) | IME candidate selection may not appear as the rename `Edit` control. Evidence is issue-title plus verified heuristic, not a verified Windows IME-state implementation. |
| Shortcuts after preview focus | [#48274](https://github.com/microsoft/PowerToys/issues/48274) (closed completed July 9, 2026) | Hosted-content focus is the localized mechanism; PR #48293 is the merged fix chronology. |
| Integer formatting overflow | [#46960](https://github.com/microsoft/PowerToys/issues/46960) | Source confirms the `Math.Abs(int.MinValue)` hazard; the exact user input path and fix status must be checked in current source. |

## Caveats and exclusions

- Several issue bodies were sparse, terse, or non-English. Rows marked by issue evidence should be
  treated as title-plus-source grounding unless a PR decision is also cited.
- `CaretVisible` intentionally fails open when `GetGUIThreadInfo` fails. That is verified behavior,
  not evidence that every activation report originates there.
- PR #48293 merged July 9, 2026; its reviewer decisions and implementation are shipped chronology.
- COM teardown observations are specific to cached `IClassFactory` RCWs, per-load preview handlers,
  `IShellItem`, streams, and CsWinRT closing callbacks; do not generalize them to unrelated previewers.
- Repo-wide infrastructure PRs were excluded as behavioral evidence: .NET 10
  ([#41280](https://github.com/microsoft/PowerToys/pull/41280)), C++/WinRT
  ([#45420](https://github.com/microsoft/PowerToys/pull/45420)), VS 2026
  ([#44304](https://github.com/microsoft/PowerToys/pull/44304)). The project-path convention from
  [#44639](https://github.com/microsoft/PowerToys/pull/44639) remains review context only.
- Pure style/formatting comments, LGTM/versioning chatter, and duplicate non-actionable “Peek not
  working” reports were excluded.
