# Workspaces Bug Triage: Symptom → Likely File/Function

Use as **hypotheses to confirm in source**, not ground truth. If a symptom doesn't map cleanly, reason
from the symptom and verify — a forced-fit map entry anchors you onto a confident-wrong file.

| Symptom | Start here | Likely cause |
|---|---|---|
| App not captured at all | `workspaces-common/WindowFilter.h` `Filter`/`FilterPopup`; `SnapshotUtils.cpp::GetApps` | Window filtered out (tool window, child, off-desktop, popup rules). |
| App captured but "not recognized" / wrong name | `AppUtils.cpp::GetApp`, `GetAppsList` | Installed-app lookup missed (packaged AUMID, install path). #43475, #46170, #46875 |
| Two similar apps confused (Edge work/personal, Sticky Notes/OneNote) | `WindowArranger.cpp::GetNearestWindow`; `PwaHelper.cpp` | Identity keyed too coarsely; PWA app-id / AUMID not resolved. #43800, #47835 |
| PWA treated as plain browser | `PwaHelper.cpp` `GetEdgeAppId`/`GetChromeAppId`/`SearchPwaName` | PWA app-id not found/stored in `Application::pwaAppId`. |
| Minimized packaged app (Settings, ToDo) missed | `GetNearestWindow` `ApplicationFrameHost` branch | Real PID not recovered via matching title. |
| Overlay/preview drawn in wrong place at non-100% DPI | `OverlayWindow.xaml.cs`, `NativeMethods.cs::SetWindowPositionDpiUnaware`, `MainViewModel.cs` | DPI-unaware coords double-scaled by WPF. #45174 / PR #45183 |
| Windows land on wrong monitor after display change | `MonitorUtils.h::IdentifyMonitors`; `WindowArranger.cpp::TryMoveWindow` | Monitor `number` no longer maps to same display. #48166, #44378 |
| Saved workspaces lost / `workspaces.json` null bytes after upgrade | `WorkspacesData.cpp` `FromJson`; `WorkspacesCsharpLibrary/Data/*`; `WorkspacesEditorIO.cs` | Schema drift or non-atomic write. #46179 / PR #44704 |
| CLI args not restored / multi-instance collapses | `CommandLineArgsHelper.cpp`, `WbemHelper.cpp`; `AppLauncher.cpp::LaunchApp` | WMI read empty/slow; identity ignores args. #43545, #49233 |
| Elevated / Store app won't launch | `AppLauncher.cpp` `LaunchApp` (`runas`), `LaunchPackagedApp` | Elevation/package-manager path. #47434 |
| Child processes linger after uninstall/disable | `WorkspacesModuleInterface/dllmain.cpp` | Spawned exes not terminated on teardown. PR #44704 |
| Editor UI black text in dark mode / clipped at large text | `WorkspacesEditor/*.xaml` | Hardcoded size / missing `Foreground` DynamicResource. PR #46172 |
| Build/hang after async refactor of helpers | `LauncherUIHelper.cpp`, `WindowArrangerHelper.cpp` | `.get()` on `IAsyncAction`; dangling coroutine ref. PR #44304, #45522 |
