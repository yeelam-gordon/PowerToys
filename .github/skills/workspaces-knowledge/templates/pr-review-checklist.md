# Workspaces PR Review Checklist

Apply after reading the diff cold (see anti-anchoring in SKILL.md). Only check rows whose files the
diff actually touches.

## Window <-> app matching (`WindowArranger.cpp`, `AppUtils.cpp`, `PwaHelper.cpp`)
- [ ] Identity gate preserved: match on `name`/`installPath` **and** `pwaAppId` before geometry.
- [ ] Browser-hosted windows resolve AUMID + PWA app-id (`GetEdgeAppId`/`GetChromeAppId`) before compare.
- [ ] `ApplicationFrameHost.exe` title-based real-PID recovery still intact (packaged/minimized apps).
- [ ] Steam thick-frame branch preserved (`!IsSteamGame() && !HasThickFrame()` skip).
- [ ] `CalculateDistance` used only as a tie-breaker, not the matcher.
- [ ] New app-type distinction has an `AppUtilsTests`/`PwaHelperTests` case.

## DPI & monitors (`OverlayWindow.xaml.cs`, `NativeMethods.cs`, `MonitorUtils.h`)
- [ ] Stored (DPI-unaware) coordinates never assigned straight to WPF `Left/Top/Width/Height`.
- [ ] Placement goes through `SetWindowPositionDpiUnaware` where DPI matters.
- [ ] Target monitor resolved via stable `id`/`instanceId`, not bare `number`; "monitor absent" handled.
- [ ] `IdentifyMonitors` retry loop left intact.

## JSON / persistence (`WorkspacesData.cpp`, `WorkspacesCsharpLibrary/Data/*`)
- [ ] C++ and C# schemas changed together; field names match.
- [ ] Every `FromJson`/deserialize defaults missing fields instead of dropping the record.
- [ ] `workspaces.json` written atomically (temp + replace).
- [ ] Round-trip test added (`JsonUtilsTests`/`WorkspacesDataTests`).

## Launch / lifecycle (`AppLauncher.cpp`, `dllmain.cpp`, `*Helper.cpp`)
- [ ] Empty/failed WMI CLI-arg reads don't drop the app.
- [ ] Elevated (`runas`), packaged, PWA (`--app-id=`), and Steam (`steam:`) launch paths considered.
- [ ] Child exes (Editor/Launcher/Arranger/Snapshot) stopped on disable/uninstall.
- [ ] No `.get()` on `winrt::IAsyncAction`; coroutine params passed by value.

## WPF Editor UI (`WorkspacesEditor/*.xaml`)
- [ ] No hardcoded `Height`/`Width` where text can enlarge — use `MinWidth`/`MinHeight`.
- [ ] `Foreground` bound to `{DynamicResource TextFillColorPrimaryBrush}` (dark-mode safe).
- [ ] Menus keyboard-reachable; correct `AutomationProperties.Name`.
- [ ] No `TwoWay`/`UpdateSourceTrigger=PropertyChanged` bindings dropped during refactor.
- [ ] No `Padding` on a `ScrollViewer` (use `Margin` on the inner control).

## General
- [ ] `$(RepoRoot)` used in project references, not `..\..\` relative paths.
- [ ] Localizable end-user strings routed through resources.
