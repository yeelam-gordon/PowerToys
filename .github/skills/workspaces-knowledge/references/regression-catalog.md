# Workspaces — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split note:** `SKILL.md` owns the operational symptom → root-cause → guardrail playbooks. This
> reference keeps only source anchors, evidence chronology, reviewer decisions, unresolved clusters,
> and evidence limits.

## Evidence caveats

- Confirm anchors against the current branch; this ledger records the reviewed source snapshot.
- Most issues contribute title-level signals, not validated mechanisms. Merged PRs and quoted review
  decisions carry more weight.
- The four-process architecture and cross-language JSON contract make apparently local evidence
  incomplete until the corresponding process/schema boundary is checked.

## Source-anchor ledger

| Area | Exact source anchors | Evidence retained | Decision or current fact | Basis |
|---|---|---|---|---|
| Process boundary and teardown | `src/modules/Workspaces/WorkspacesModuleInterface/dllmain.cpp` child executable paths and enable/disable flow; `WorkspacesLib/IPCHelper.cpp`; `WorkspacesLib/two_way_pipe_message_ipc.cpp` | [PR #44704](https://github.com/microsoft/PowerToys/pull/44704) | Snapshot, Editor, Launcher, and Window Arranger are separate processes; teardown decisions must account for spawned executables. PR #44704 included stopping the new executable on uninstall. | Source + merged PR |
| Browser/PWA identity | `WorkspacesLib/AppUtils.cpp::GetApp`; `WorkspacesLib/PwaHelper.cpp::GetEdgeAppId`, `::GetChromeAppId`, `::SearchPwaName`; `WorkspacesWindowArranger/WindowArranger.cpp::GetNearestWindow` | [#43800](https://github.com/microsoft/PowerToys/issues/43800), [#44172](https://github.com/microsoft/PowerToys/issues/44172) | The matching predicate retains name-or-install-path plus equal `pwaAppId`; browser-hosted identity is not process-name-only. | Source + issue signal |
| Packaged/UWP identity | `WindowArranger.cpp::GetNearestWindow` `ApplicationFrameHost.exe` branch; `WorkspacesLib/AppUtils.cpp::GetApp(HWND,...)` | [#47835](https://github.com/microsoft/PowerToys/issues/47835), [#44510](https://github.com/microsoft/PowerToys/issues/44510), [#46004](https://github.com/microsoft/PowerToys/issues/46004) | The title-based different-PID recovery for `ApplicationFrameHost.exe` remains part of the identity path. | Source + issue signal |
| Other app-recognition coverage | `WorkspacesLib/AppUtils.cpp`; `workspaces-common/WindowFilter.h::Filter`, `::FilterPopup` | [#43475](https://github.com/microsoft/PowerToys/issues/43475), [#46170](https://github.com/microsoft/PowerToys/issues/46170), [#46875](https://github.com/microsoft/PowerToys/issues/46875) | These artifacts identify recognition gaps; they do not establish one shared cause. | Issue signal |
| Steam matching | `WorkspacesWindowArranger/WindowArranger.cpp::GetNearestWindow`; `WorkspacesLib/SteamGameHelper.cpp`; `AppData::IsSteamGame` | — | The candidate filter retains the Steam/thick-frame distinction; this is legacy behavior, not a generalized identity rule. | Source |
| Coordinate model | `workspaces-common/MonitorUtils.h::IdentifyMonitors`; `WorkspacesEditor/Utils/NativeMethods.cs::SetWindowPositionDpiUnaware`; `WorkspacesEditor/OverlayWindow.xaml.cs` | [#45174](https://github.com/microsoft/PowerToys/issues/45174), [PR #45183](https://github.com/microsoft/PowerToys/pull/45183) | `Monitor` keeps DPI-aware and DPI-unaware rectangles; stored positions remain DPI-unaware. The accepted editor placement path temporarily uses a DPI-unaware context around `SetWindowPos`. | Source + merged fix |
| Monitor identity and enumeration | `workspaces-common/MonitorUtils.h::IdentifyMonitors`; `workspaces-common/MonitorEnumerator.h`; `WorkspacesWindowArranger/WindowArranger.cpp::TryMoveWindow`, `::CalculateDistance` | [#48166](https://github.com/microsoft/PowerToys/issues/48166), [#44378](https://github.com/microsoft/PowerToys/issues/44378), [#45567](https://github.com/microsoft/PowerToys/issues/45567) | `number` is positional; `id`/`instanceId` are the available stable identity fields. `IdentifyMonitors` retains the 100 × 30 ms retry for transient empty display enumeration. | Source + issue signal |
| C++ JSON contract | `WorkspacesLib/WorkspacesData.cpp` `WorkspacesProjectJSON::FromJson`, `WorkspacesListJSON::FromJson`, `AppLaunchInfoJSON::FromJson` | [#46179](https://github.com/microsoft/PowerToys/issues/46179) | Fields are parsed through `std::optional`; compatibility decisions must be made with missing/renamed fields in mind. | Source + issue signal |
| C# JSON contract | `WorkspacesCsharpLibrary/Data/ApplicationWrapper.cs`; `WorkspacesStorage.cs`; `WorkspacesStorageJsonContext.cs`; `Utils/DashCaseNamingPolicy.cs`; `WorkspacesEditor/Utils/WorkspacesEditorIO.cs` | [PR #44704](https://github.com/microsoft/PowerToys/pull/44704), [#46179](https://github.com/microsoft/PowerToys/issues/46179) | C# and C++ schemas remain parallel contracts. PR #44704 is retained as the deserialization decision point; the null-byte report remains unresolved evidence for write durability. | Source + merged PR + issue signal |
| Command-line capture | `WorkspacesLib/CommandLineArgsHelper.cpp::GetCommandLineArgs`; `WorkspacesLib/WbemHelper.cpp`; `WorkspacesLauncher/AppLauncher.cpp::LaunchApp` | [#43545](https://github.com/microsoft/PowerToys/issues/43545), [#49233](https://github.com/microsoft/PowerToys/issues/49233) | Args are captured per PID through WMI and consumed at launch; current window identity does not key on command-line args. | Source + issue signal |
| Launch variants | `WorkspacesLauncher/AppLauncher.cpp::Launch`, `::LaunchApp`, `::LaunchPackagedApp` | [#47434](https://github.com/microsoft/PowerToys/issues/47434), [#47068](https://github.com/microsoft/PowerToys/issues/47068) | The launch ledger includes elevated `runas`, packaged-app activation, browser PWA `--app-id=`, and Steam protocol paths; issue titles do not isolate which branch failed. | Source + issue signal |
| Coroutine migration | `WorkspacesLauncher/LauncherUIHelper.cpp`; `WorkspacesLauncher/WindowArrangerHelper.cpp` | [PR #44304](https://github.com/microsoft/PowerToys/pull/44304), [PR #45522](https://github.com/microsoft/PowerToys/pull/45522) | Review rejected `.get()` on `winrt::IAsyncAction` and by-reference/out-parameter state crossing `co_await`; the accepted direction used value lifetimes and `async_task<T>` returns. | Reviewer decision |
| WPF Fluent migration | `WorkspacesEditor/MainWindow.xaml`; `MainPage.xaml`; `WorkspacesEditorPage.xaml`; `Models/Project.cs` | [PR #46172](https://github.com/microsoft/PowerToys/pull/46172) | Review decisions: use minimum rather than fixed dimensions for text scaling; dynamic theme brushes; inner-control margin rather than `ScrollViewer.Padding`; keyboard-reachable menus; correct automation names; preserve two-way immediate search binding. Naive month/year elapsed-time arithmetic was flagged. | Reviewer decision |
| Project references/toolset | Workspaces project references; centrally defined `PlatformToolset` | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) | Review standardized `$(RepoRoot)` references; DHowett review rejected per-project `PlatformToolset` duplication. | Reviewer decision |

## Decision chronology

| Date | Artifact | Recorded decision |
|---|---|---|
| 2026-01-13 | [PR #44704](https://github.com/microsoft/PowerToys/pull/44704) merged | Corrected editor deserialization and included spawned-executable shutdown on uninstall. |
| 2026-01-28 | [PR #44304](https://github.com/microsoft/PowerToys/pull/44304) merged | Coroutine review established that `winrt::IAsyncAction` is not a `std::future` and cannot use `.get()`. |
| 2026-02-02 | [PR #45183](https://github.com/microsoft/PowerToys/pull/45183) merged | Fixed snapshot overlay placement through the DPI-unaware positioning path. |
| 2026-02-07 | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) merged | Standardized project references on `$(RepoRoot)`. |
| 2026-02-11 | [PR #45522](https://github.com/microsoft/PowerToys/pull/45522) merged | Reworked async APIs around return values/value lifetimes rather than dangling coroutine references. |
| 2026-05-21 | [PR #46172](https://github.com/microsoft/PowerToys/pull/46172) merged | Completed WPF Fluent migration subject to accessibility, binding, theme, and layout review decisions. |

## Unresolved evidence clusters

| Cluster | Open evidence | What remains unresolved |
|---|---|---|
| App identity/recognition | [#43800](https://github.com/microsoft/PowerToys/issues/43800), [#47835](https://github.com/microsoft/PowerToys/issues/47835), [#44172](https://github.com/microsoft/PowerToys/issues/44172), [#43475](https://github.com/microsoft/PowerToys/issues/43475), [#46170](https://github.com/microsoft/PowerToys/issues/46170), [#46875](https://github.com/microsoft/PowerToys/issues/46875), [#44510](https://github.com/microsoft/PowerToys/issues/44510), [#46004](https://github.com/microsoft/PowerToys/issues/46004) | Which failures share identity logic versus filtering, package metadata, minimized-window, or launch-data causes. |
| Display topology | [#48166](https://github.com/microsoft/PowerToys/issues/48166), [#44378](https://github.com/microsoft/PowerToys/issues/44378), [#45567](https://github.com/microsoft/PowerToys/issues/45567) | Stable remapping policy when captured displays are missing, renumbered, or changed by FancyZones/topology updates. |
| Persistence durability | [#46179](https://github.com/microsoft/PowerToys/issues/46179) (closed completed May 22, 2026) | Historical null-byte evidence; the precise writer/failure path and atomic-write coverage still require source verification before reuse. |
| Per-instance arguments and launch | [#43545](https://github.com/microsoft/PowerToys/issues/43545), [#49233](https://github.com/microsoft/PowerToys/issues/49233), [#47434](https://github.com/microsoft/PowerToys/issues/47434), [#47068](https://github.com/microsoft/PowerToys/issues/47068) | How identity should distinguish same-app instances and which launch branches fail after app updates or minimized startup. |

## Scope exclusions

Pipeline chatter, spell-check reports, celebratory/LGTM comments, pixel-level XAML nits, and unrelated
toolchain upgrades were excluded because they do not establish durable Workspaces decisions.
