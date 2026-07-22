---
name: powertoys-workspaces-knowledge
description: 'PowerToys Workspaces module knowledge: feature->file/function map, recurring regression playbooks (window<->app matching identity for Edge/Chrome PWA & packaged/ApplicationFrameHost & Steam apps, DPI-aware vs DPI-unaware coordinate storage, monitor-number remapping on display changes, CLI-arg capture via WMI, elevated/packaged launch, JSON snapshot round-trip & deserialization), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/Workspaces — capturing/snapshotting a desktop layout, launching a workspace, window placement/monitor assignment, app-launch/CLI args, the WPF editor, or the launcher status UI. Keywords: Workspaces, snapshot, launcher, window arranger, WindowFilter, PWA, AUMID, DPI, monitor, WMI command line, WPF Fluent, JSON, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Workspaces Knowledge

Grounded engineering knowledge for the PowerToys **Workspaces** module — captures a set of running
desktop applications plus their per-monitor window positions/states into a named "workspace"
(`workspaces.json`), then relaunches those apps and re-arranges their windows onto the correct
monitors. It has four cooperating executables: the **Snapshot tool** (capture), the **Editor** (WPF
UI to edit/preview), the **Launcher** (spawn apps), and the **Window Arranger** (match spawned
windows back to captured apps and move them). Use this to localize code fast, avoid known regression
traps, and enforce the conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/Workspaces/` and needing prior art.
- Fixing/triaging a Workspaces bug: an app isn't captured or isn't recognized, two similar apps get
  confused (Edge work vs personal, Sticky Notes vs OneNote, a PWA vs its browser), windows land on
  the wrong monitor after a display change, overlay/preview draws in the wrong place at non-100% DPI,
  CLI args aren't restored, elevated/packaged apps don't launch, or `workspaces.json` is corrupted.
- Reviewing a Workspaces PR against maintainer conventions and regression traps.
- Touching capture (`SnapshotUtils::GetApps`), window matching (`WindowArranger::GetNearestWindow`),
  app identity (`AppUtils`, `PwaHelper`), monitor identification, DPI handling, or the WPF editor UI.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Module entry, hotkeys (editor / snapshot / launcher), enable/disable, spawn child exes | `WorkspacesModuleInterface/dllmain.cpp` (`workspacesEditorPath`, `workspacesSnapshotToolPath`, `workspacesLauncherPath`, `workspacesWindowArrangerPath`; hotkey JSON keys `hotkey`/`run-snapshot-tool-hotkey`/`run-launcher-hotkey`) |
| Shared data model + JSON (de)serialization | `WorkspacesLib/WorkspacesData.{h,cpp}` `WorkspacesProject` (`Application`, `Monitor`, `Position`), `WorkspacesProjectJSON`/`WorkspacesListJSON`/`AppLaunchInfoJSON`; file paths `WorkspacesFile()`, `TempWorkspacesFile()` |
| **Capture** a workspace (enumerate windows -> apps) | `WorkspacesSnapshotTool/SnapshotUtils.cpp` `GetApps`; entry `WorkspacesSnapshotTool/main.cpp` |
| Window eligibility filter (what gets captured/matched) | `workspaces-common/WindowFilter.h` `Filter`, `FilterPopup`; helpers `workspaces-common/WindowUtils.h`, `WorkspacesLib/WindowUtils.cpp` |
| Window enumeration | `workspaces-common/WindowEnumerator.h`; `workspaces-common/VirtualDesktop.h` `IsWindowOnCurrentDesktop` |
| App identity resolution (installed-app list, match by path/PID/window) | `WorkspacesLib/AppUtils.cpp` `GetAppsList`, `GetApp(path,pid,...)`, `GetApp(HWND,...)`; `AppData::IsEdge/IsChrome/IsSteamGame`; `UpdateWorkspacesApps`/`UpdateAppVersion` |
| PWA identity (Edge/Chrome installed web apps) | `WorkspacesLib/PwaHelper.cpp` `GetEdgeAppId`, `GetChromeAppId`, `SearchPwaName`, `InitEdgeAppIds`/`InitChromeAppIds` |
| Command-line-args capture (per process, via WMI) | `WorkspacesLib/CommandLineArgsHelper.{h,cpp}` `GetCommandLineArgs`; WMI wrapper `WorkspacesLib/WbemHelper.cpp` |
| Steam game detection/launch | `WorkspacesLib/SteamGameHelper.cpp`, `SteamHelper.h`; `AppData::IsSteamGame` |
| Monitor identification (DPI-aware & DPI-unaware rects) | `workspaces-common/MonitorUtils.h` `IdentifyMonitors` (wraps `DisplayUtils::GetDisplays`), `MonitorEnumerator.h` |
| **Launch** orchestration | `WorkspacesLauncher/Launcher.cpp` `Launch`; entry `WorkspacesLauncher/main.cpp`; invoke source `workspaces-common/InvokePoint.h` |
| App launch (shell exec, elevated `runas`, packaged, PWA, Steam) | `WorkspacesLauncher/AppLauncher.cpp` `Launch`, `LaunchApp`, `LaunchPackagedApp` (Edge/Chrome PWA `--app-id=`, `steam:` protocol) |
| Launcher <-> UI / arranger IPC | `WorkspacesLauncher/LauncherUIHelper.cpp`, `WindowArrangerHelper.cpp`; `WorkspacesLib/IPCHelper.cpp`, `two_way_pipe_message_ipc.cpp` |
| Launch-progress state machine | `WorkspacesLib/LaunchingStatus.cpp`, `LaunchingStateEnum.h` |
| **Arrange** — match launched windows to captured apps & move them | `WorkspacesWindowArranger/WindowArranger.cpp` `GetNearestWindow`, `TryMoveWindow`, `processWindows`; `CalculateDistance`; entry `WorkspacesWindowArranger/main.cpp` |
| Window property tweaks on placement | `WindowProperties/WorkspacesWindowPropertyUtils.h` |
| Editor app (WPF) — main window/pages, snapshot preview overlay | `WorkspacesEditor/MainWindow.xaml`, `MainPage.xaml`, `WorkspacesEditorPage.xaml`, `SnapshotWindow.xaml`, `OverlayWindow.xaml(.cs)` |
| Editor view model, IO, DPI overlay positioning | `WorkspacesEditor/ViewModels/MainViewModel.cs`, `Utils/WorkspacesEditorIO.cs`, `Utils/NativeMethods.cs` (`SetWindowPositionDpiUnaware`), `Utils/MonitorHelper.cs`, `Utils/DrawHelper.cs` |
| Launcher status UI (WPF) | `WorkspacesLauncherUI/StatusWindow.xaml`, `ViewModels/MainViewModel.cs` |
| Shared C# model + storage + JSON | `WorkspacesCsharpLibrary/Data/*` (`ProjectData.cs`, `ApplicationWrapper.cs`, `WorkspacesStorage.cs`, `WorkspacesStorageJsonContext.cs`), `PwaHelper.cs`, `Utils/DashCaseNamingPolicy.cs` |
| Telemetry | `WorkspacesLib/trace.cpp`; `WorkspacesEditor/Telemetry/*` |
| Tests | `WorkspacesLib.UnitTests/*` (`AppUtilsTests`, `JsonUtilsTests`, `PwaHelperTests`, `WorkspacesDataTests`), UI tests `WorkspacesEditorUITest/*` |

**Two coordinate systems, always both stored.** Each `Monitor` carries **both**
`monitorRectDpiAware` **and** `monitorRectDpiUnaware`, and window `Position` is stored in
**DPI-unaware** coordinates (captured on a temporary DPI-unaware thread) so positions survive across
machines with different scaling. The WPF Editor is PerMonitorV2 DPI-aware, so any place that feeds
stored coordinates into WPF must bypass WPF's re-scaling (see DPI playbook).

**Window<->app matching is identity + geometry.** `GetNearestWindow` first matches on *identity*
(`app.name == appData.name || app.path == appData.installPath`, **and** `app.pwaAppId ==
appData.pwaAppId`), then picks the geometrically nearest candidate via `CalculateDistance`. Both
halves have caused regressions.

## Regression Playbooks

Rule by rule: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Wrong/duplicate app identity — two similar apps confused, or an app not recognized
- **Symptom:** work vs personal Edge profiles collapse into one; a PWA is captured/launched as plain
  Edge/Chrome; Sticky Notes launches OneNote; KeePass/Bitwarden/3CX/Store apps not recognized;
  packaged apps missing when minimized.
- **Where:** `WindowArranger.cpp::GetNearestWindow` (identity test on `name`/`installPath`/`pwaAppId`);
  `AppUtils.cpp::GetApp` + `AppData::IsEdge/IsChrome`; `PwaHelper.cpp` (`GetEdgeAppId`,
  `GetChromeAppId`, `SearchPwaName`); the `ApplicationFrameHost` special-case in `GetNearestWindow`.
- **Root cause:** identity keyed on coarse fields (process name / install path). Browsers share one
  exe across profiles and PWAs; UWP/packaged apps host under `ApplicationFrameHost.exe`, so the real
  process must be recovered by matching the window title of a different PID.
- **Guardrail:** for browser-hosted windows, resolve the AUMID and PWA app-id **before** comparing
  identity, and include `pwaAppId` in the match key (already required by the `&&` in
  `GetNearestWindow`); keep the `ApplicationFrameHost` title-based PID recovery intact when touching
  capture/matching. Any new app-type distinction needs an `AppUtilsTests`/`PwaHelperTests` case.
  Evidence: issues [#43800](https://github.com/microsoft/PowerToys/issues/43800) (Edge work vs
  personal), [#47835](https://github.com/microsoft/PowerToys/issues/47835) (Sticky Notes as OneNote),
  [#43475](https://github.com/microsoft/PowerToys/issues/43475) (KeePass),
  [#46170](https://github.com/microsoft/PowerToys/issues/46170) (Bitwarden),
  [#46875](https://github.com/microsoft/PowerToys/issues/46875) (3CX),
  [#44172](https://github.com/microsoft/PowerToys/issues/44172) (Preview PowerShell as Terminal).

### DPI-unaware coordinates double-scaled by WPF (overlay/preview mis-positioned)
- **Symptom:** the snapshot overlay / preview draws in the wrong place or on the wrong region at
  non-100% scaling or multi-DPI setups.
- **Where:** `WorkspacesEditor/OverlayWindow.xaml.cs`, `ViewModels/MainViewModel.cs`,
  `Utils/NativeMethods.cs` `SetWindowPositionDpiUnaware`.
- **Root cause:** stored positions are DPI-*unaware*, but the Editor is PerMonitorV2 DPI-aware;
  assigning them to WPF window properties makes WPF scale them a second time.
- **Guardrail:** never assign stored (DPI-unaware) coordinates straight to WPF `Left/Top/Width/Height`;
  route through `SetWindowPositionDpiUnaware` (temporarily enter a DPI-unaware context around
  `SetWindowPos`). Evidence: issue [#45174](https://github.com/microsoft/PowerToys/issues/45174); fix
  [PR #45183](https://github.com/microsoft/PowerToys/pull/45183). Related open:
  [#45567](https://github.com/microsoft/PowerToys/issues/45567).

### Windows land on the wrong monitor after a display change
- **Symptom:** on relaunch, windows don't move to the intended external monitor; monitor assignment is
  off when the monitor count/arrangement changed since capture.
- **Where:** `MonitorUtils.h::IdentifyMonitors`, `Monitor` (`number`, `id`, `instanceId`, both rects);
  arranger geometry `WindowArranger.cpp::TryMoveWindow`/`CalculateDistance`.
- **Root cause:** captured `monitor` **number** is positional; monitor numbering/identity can change
  between capture and launch (hot-plug, resolution/DPI change), so a stored number no longer maps to
  the same physical display. `IdentifyMonitors` already retries up to 100×30ms because displays report
  transiently empty.
- **Guardrail:** prefer stable identity (`id`/`instanceId`) over bare `number` when resolving the
  target monitor; handle the "captured monitor no longer present" case explicitly rather than trusting
  the index. Evidence: issues
  [#48166](https://github.com/microsoft/PowerToys/issues/48166),
  [#44378](https://github.com/microsoft/PowerToys/issues/44378),
  [#45174](https://github.com/microsoft/PowerToys/issues/45174).

### `workspaces.json` corruption / deserialization failure loses all workspaces
- **Symptom:** after an upgrade or a bad write, `workspaces.json` contains null bytes / can't parse and
  all saved workspaces disappear; an editor field fails to deserialize.
- **Where:** C++ `WorkspacesData.cpp` `*JSON::FromJson`; C# `WorkspacesCsharpLibrary/Data/*`
  (`ApplicationWrapper.cs`, `WorkspacesStorage.cs`, `WorkspacesStorageJsonContext.cs`), IO
  `WorkspacesEditor/Utils/WorkspacesEditorIO.cs`.
- **Root cause:** `FromJson` returns `std::optional` per field; a single missing/renamed key or a
  non-atomic/partial write silently drops data or nulls the file.
- **Guardrail:** keep C++ and C# schemas in lockstep; treat every `FromJson`/deserialize as fallible
  and default missing fields instead of discarding the record; write via temp file + atomic replace.
  Add a `JsonUtilsTests`/`WorkspacesDataTests` round-trip case for any new field. Evidence: issues
  [#46179](https://github.com/microsoft/PowerToys/issues/46179) (null bytes after upgrade); fix
  [PR #44704](https://github.com/microsoft/PowerToys/pull/44704) (editor deserialization issue + stop
  new exe on uninstall).

### CLI args not captured/restored; same app with different args
- **Symptom:** an app relaunches without its command-line args; multiple instances of the same app
  (e.g. Edge with different `--app`/profile args) collapse or restore wrong.
- **Where:** `CommandLineArgsHelper.cpp::GetCommandLineArgs` (WMI via `WbemHelper.cpp`); stored in
  `Application::commandLineArgs`; consumed by `AppLauncher.cpp::LaunchApp`.
- **Root cause:** args are read from WMI per-PID at capture time; WMI can be slow/unavailable or return
  nothing, and identity matching (above) doesn't key on args so two instances look identical.
- **Guardrail:** tolerate empty/failed WMI reads without dropping the app; when distinguishing multiple
  instances, remember the current identity test ignores args. Evidence: issues
  [#43545](https://github.com/microsoft/PowerToys/issues/43545) (multi-instance different CLI args),
  [#49233](https://github.com/microsoft/PowerToys/issues/49233).

### WinRT coroutine migration — IPC helpers broken by `.get()` / dangling refs
- **Symptom:** build breaks or async IPC hangs after migrating launcher/arranger helpers to WinRT
  coroutines.
- **Where:** `WorkspacesLauncher/LauncherUIHelper.cpp`, `WindowArrangerHelper.cpp` (async waits).
- **Root cause:** `winrt::IAsyncAction` has **no** `.get()` (unlike `std::future`); coroutine params
  taken by `const&` dangle after the first `co_await`.
- **Guardrail:** don't call `.get()` on `IAsyncAction`; take coroutine parameters **by value** (or
  guarantee lifetime) so nothing dangles across a suspension point. Evidence: Copilot review on
  [PR #44304](https://github.com/microsoft/PowerToys/pull/44304) (`.get()` on `IAsyncAction`) and
  [PR #45522](https://github.com/microsoft/PowerToys/pull/45522) (out-param/by-ref coroutine pitfalls).

## Review Rules

Enforce these when reviewing or authoring Workspaces changes:

- **Match window identity before geometry.** Any change to `GetNearestWindow`/`GetApp` must preserve
  the identity gate (name/installPath **and** `pwaAppId`) and the `ApplicationFrameHost` title-based
  PID recovery; distance is a tie-breaker, not the matcher. Evidence:
  [#43800](https://github.com/microsoft/PowerToys/issues/43800),
  [#47835](https://github.com/microsoft/PowerToys/issues/47835).
- **Resolve PWA app-id for browser-hosted windows** via `PwaHelper` before comparing, and store it in
  `Application::pwaAppId`. Don't treat a PWA as its host browser. Evidence:
  [#43800](https://github.com/microsoft/PowerToys/issues/43800).
- **Never feed DPI-unaware stored coordinates directly to WPF.** Use
  `NativeMethods.SetWindowPositionDpiUnaware`; the Editor is PerMonitorV2 and will double-scale.
  Evidence: [PR #45183](https://github.com/microsoft/PowerToys/pull/45183).
- **Prefer stable monitor identity over `number`.** When resolving a target display, use
  `id`/`instanceId` and handle "monitor no longer present"; don't assume the index still maps.
  Evidence: [#48166](https://github.com/microsoft/PowerToys/issues/48166).
- **Keep C++ and C# JSON schemas in lockstep and every `FromJson` tolerant.** A missing key must
  default, not drop the record or null the file; add a round-trip test. Evidence:
  [#46179](https://github.com/microsoft/PowerToys/issues/46179),
  [PR #44704](https://github.com/microsoft/PowerToys/pull/44704).
- **Write `workspaces.json` atomically** (temp + replace) so a crash mid-write can't corrupt saved
  workspaces. Evidence: [#46179](https://github.com/microsoft/PowerToys/issues/46179).
- **Don't call `.get()` on `winrt::IAsyncAction`; pass coroutine params by value.** Evidence:
  [PR #44304](https://github.com/microsoft/PowerToys/pull/44304),
  [PR #45522](https://github.com/microsoft/PowerToys/pull/45522).
- **Stop the spawned child exes on module disable/uninstall.** Editor/Launcher/Arranger/Snapshot are
  separate processes launched from `dllmain.cpp`; leaking them on teardown is a defect. Evidence:
  [PR #44704](https://github.com/microsoft/PowerToys/pull/44704).
- **WPF Editor UI: don't hardcode sizes; honor theme + accessibility.** Use `MinWidth`/`MinHeight` not
  fixed `Height` (breaks at enlarged Windows text size), bind `Foreground` to
  `{DynamicResource TextFillColorPrimaryBrush}` (else black in dark mode), keep menus keyboard-reachable,
  set correct `AutomationProperties.Name`, and don't drop `TwoWay`/`UpdateSourceTrigger=PropertyChanged`
  bindings during refactors. Evidence: review on
  [PR #46172](https://github.com/microsoft/PowerToys/pull/46172).
- **Ship a test with matching/JSON fixes.** Suites live in `WorkspacesLib.UnitTests`
  (`AppUtilsTests`, `PwaHelperTests`, `JsonUtilsTests`, `WorkspacesDataTests`); UI in
  `WorkspacesEditorUITest`.

## Pitfalls

- **Never** compare app identity by process name alone — Edge/Chrome share one exe across profiles and
  PWAs, and UWP apps host under `ApplicationFrameHost.exe`. Resolve AUMID/PWA app-id and recover the
  real PID by title first (#43800, #47835).
- **Positions are stored DPI-unaware; the Editor is DPI-aware.** Assigning stored coordinates to WPF
  double-scales them — always go through `SetWindowPositionDpiUnaware` (#45174 / PR #45183).
- **Monitor `number` is not stable** across hot-plug / resolution / DPI changes; a captured index may
  point at a different physical display on relaunch (#48166, #44378).
- **`IdentifyMonitors` retries up to 100×30ms** because `GetDisplays` returns transiently empty right
  after a display change — don't "simplify" the retry away.
- **`WindowFilter::Filter` deliberately excludes** tool windows, non-root/child windows, invisible
  windows, and windows off the current virtual desktop; `FilterPopup` re-includes caption/min-max
  popups (Calculator, Telegram) but drops menus/start/tray. Changing these predicates silently changes
  what gets captured.
- **CLI args come from WMI** (`WbemHelper`) and can be empty/slow — never drop an app because its args
  read failed (#43545).
- **`winrt::IAsyncAction` has no `.get()`**, and by-`const&` coroutine params dangle after `co_await`
  (PR #44304, #45522).
- **`workspaces.json` has parallel C++ and C# schemas.** A field added on one side but not the other
  breaks round-trip and can null the file on upgrade (#46179 / PR #44704).
- **Steam games only match when they lack a thick frame** — `GetNearestWindow` skips non-Steam windows
  without `WS_THICKFRAME` to preserve legacy behavior; keep that branch when editing matching.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**; then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you on recurring
themes and measurably lowers your catch rate on the PR's actual issues. If a symptom doesn't map to
a row, reason from the source, not the map. Best for planning / triage; a targeted checklist (not a
script) for review.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Workspaces PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/Workspaces/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/Workspaces)
- [Per-Monitor DPI awareness](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows) · [Application User Model IDs (AUMID)](https://learn.microsoft.com/en-us/windows/win32/shell/appids) · [WMI Win32_Process CommandLine](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process) · [winrt::IAsyncAction](https://learn.microsoft.com/en-us/windows/uwp/cpp-and-winrt-apis/concurrency)
