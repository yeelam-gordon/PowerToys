# Workspaces Regression Catalog (progressive disclosure)

Fuller list of grounded regressions, decisions, and conventions for the PowerToys **Workspaces**
module. Load from SKILL.md when the touched area needs deeper history. Every entry cites a real
PR/issue; treat file/function pointers as hypotheses to confirm in source.

## Architecture (four cooperating processes)

`WorkspacesModuleInterface/dllmain.cpp` spawns four separate executables, coordinated by hotkeys and
IPC (`IPCHelper`, `two_way_pipe_message_ipc`):
- **Snapshot tool** (`WorkspacesSnapshotTool`) — captures running apps/windows (`SnapshotUtils::GetApps`).
- **Editor** (`WorkspacesEditor`, WPF) — edit/preview/save workspaces; overlay preview at capture positions.
- **Launcher** (`WorkspacesLauncher`) — launches captured apps (`AppLauncher::Launch`) and drives status UI.
- **Window Arranger** (`WorkspacesWindowArranger`) — matches launched windows to captured apps and moves them.

Consequence: teardown must stop all four; the data contract (`WorkspacesData` C++ / `WorkspacesCsharpLibrary`
C#) must stay identical across process and language boundaries.

## Regression classes (detail)

### 1. App identity / recognition
- Browsers (Edge/Chrome) share one exe across profiles and PWAs — identity must include the resolved
  PWA app-id and AUMID, not just process name/path. `GetNearestWindow` compares
  `(app.name == appData.name || app.path == appData.installPath) && (app.pwaAppId == appData.pwaAppId)`.
- UWP/packaged apps host under `ApplicationFrameHost.exe`; `GetNearestWindow` recovers the real process
  by finding another window with the same title but a different PID. Keep this when editing capture.
- Steam games: `GetNearestWindow` only considers non-Steam windows that have a thick frame
  (`!data->IsSteamGame() && !WindowUtils::HasThickFrame(window)` → skip), preserving legacy behavior.
- Evidence: [#43800](https://github.com/microsoft/PowerToys/issues/43800) Edge company vs individual;
  [#47835](https://github.com/microsoft/PowerToys/issues/47835) Sticky Notes as OneNote;
  [#44172](https://github.com/microsoft/PowerToys/issues/44172) Preview PowerShell as Terminal;
  [#43475](https://github.com/microsoft/PowerToys/issues/43475) KeePass;
  [#46170](https://github.com/microsoft/PowerToys/issues/46170) Bitwarden;
  [#46875](https://github.com/microsoft/PowerToys/issues/46875) 3CX;
  [#44510](https://github.com/microsoft/PowerToys/issues/44510) Win10 Calculator/Settings captured, VPN not;
  [#46004](https://github.com/microsoft/PowerToys/issues/46004) ghost Settings app in snapshot.

### 2. DPI / coordinates
- Positions stored DPI-unaware (captured on a temporary DPI-unaware thread); each `Monitor` keeps both
  `monitorRectDpiAware` and `monitorRectDpiUnaware`. Editor is PerMonitorV2 DPI-aware.
- Assigning stored coords to WPF window properties double-scales; use `SetWindowPositionDpiUnaware`
  (temporarily DPI-unaware around `SetWindowPos`).
- Evidence: [#45174](https://github.com/microsoft/PowerToys/issues/45174) snapshot creator captured wrong
  monitor region; fix [PR #45183](https://github.com/microsoft/PowerToys/pull/45183).

### 3. Monitors / multi-display
- `IdentifyMonitors` wraps `DisplayUtils::GetDisplays` and retries up to 100×30ms (displays report empty
  transiently after changes). `Monitor` carries `number`, `id`, `instanceId`, `dpi`.
- `number` is positional and unstable across hot-plug / resolution / DPI changes.
- Evidence: [#48166](https://github.com/microsoft/PowerToys/issues/48166) not moved to external monitors
  due to changing screen numbers; [#44378](https://github.com/microsoft/PowerToys/issues/44378) FancyZones
  interaction; [#45567](https://github.com/microsoft/PowerToys/issues/45567) window-switching in a zone.

### 4. Persistence / JSON round-trip
- Parallel schemas: C++ `WorkspacesData.cpp` `*JSON::FromJson` (each field `std::optional`) and C#
  `WorkspacesCsharpLibrary/Data/*` with `WorkspacesStorageJsonContext` + `DashCaseNamingPolicy`.
- Missing/renamed key or non-atomic write drops data or nulls the file.
- Evidence: [#46179](https://github.com/microsoft/PowerToys/issues/46179) config lost after upgrade,
  `workspaces.json` filled with null bytes; [PR #44704](https://github.com/microsoft/PowerToys/pull/44704)
  editor deserialization fix + stop new exe on uninstall (touched `ApplicationWrapper.cs`).

### 5. Launch (CLI args, elevation, packaged, PWA, Steam)
- `AppLauncher::LaunchApp` uses `ShellExecuteEx` with `runas` when `elevated`; PWA via
  `--profile-directory=Default --app-id=`; Steam via `steam:` protocol; packaged via `PackageManager`.
- CLI args captured per-PID from WMI (`CommandLineArgsHelper`/`WbemHelper`); can be empty/slow.
- Evidence: [#43545](https://github.com/microsoft/PowerToys/issues/43545) multi-instance different CLI args;
  [#49233](https://github.com/microsoft/PowerToys/issues/49233) updated app not opening;
  [#47434](https://github.com/microsoft/PowerToys/issues/47434) not launching / no shortcut;
  [#47068](https://github.com/microsoft/PowerToys/issues/47068) start minimized.

### 6. Async / WinRT coroutine migration
- `winrt::IAsyncAction` has no `.get()`; by-`const&` coroutine params dangle after `co_await`.
- Evidence: Copilot review on [PR #44304](https://github.com/microsoft/PowerToys/pull/44304)
  (`.get()` on `IAsyncAction` in `LauncherUIHelper.cpp`/`WindowArrangerHelper.cpp`) and
  [PR #45522](https://github.com/microsoft/PowerToys/pull/45522) (out-param/by-ref coroutine lifetime;
  migrated to `async_task<T>` return types).

## Key decisions & conventions (from PR review)

- **WPF Fluent migration** ([PR #46172](https://github.com/microsoft/PowerToys/pull/46172)): dropped
  `ControlzEx`/`ModernWpf` for built-in WPF Fluent theming (Mica, default styles). Note: net binary
  savings were negligible because `ControlzEx` is still pulled transitively via `Common.UI`.
  Review-enforced UI conventions: no hardcoded sizes (use `MinWidth`/`MinHeight` — breaks at enlarged
  Windows text size); bind `Foreground` to `{DynamicResource TextFillColorPrimaryBrush}` (else black in
  dark mode); no `Padding` on `ScrollViewer` (use `Margin` on inner control); menus must be
  keyboard-reachable; correct `AutomationProperties.Name`; don't drop
  `Text="{Binding SearchTerm, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"` during XAML refactors
  (broke search filtering, fixed commit 55f9664a).
- **`LastLaunched` time math** ([PR #46172](https://github.com/microsoft/PowerToys/pull/46172) review):
  `Models/Project.cs` used naive 24h/30-day/365-day arithmetic — flagged as imprecise for month/year.
- **`$(RepoRoot)` in project references, not `..\..\`** ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639));
  don't add per-vcxproj `PlatformToolset` (unified centrally — DHowett review).
- **Ship tests**: `WorkspacesLib.UnitTests` (`AppUtilsTests`, `PwaHelperTests`, `JsonUtilsTests`,
  `WorkspacesDataTests`); UI in `WorkspacesEditorUITest`.

## Excluded as noise (not distilled)
Pure XAML nits (exact margin/padding pixel values, "nit: 5 or 4?", tooltip contrast, single-symbol
suggestions), `/azp run` pipeline chatter, check-spelling-bot reports, "Amazing work"/LGTM, and
build-toolset bumps unrelated to Workspaces logic (VS2026/CppWinRT/.NET10 upgrades) — none generalize to
future Workspaces work.
