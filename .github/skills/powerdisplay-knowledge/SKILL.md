---
name: powerdisplay-knowledge
description: 'PowerToys PowerDisplay (Power Display) module knowledge: feature->file/function map, regression playbooks (DDC/CI capability-fetch BSOD blacklist + auto-disable/crash-lock, false-positive crash detection on cooperative exit, monitor classification WMI-first vs DDC/CI, discrete-GPU internal-panel detection, stable monitor-Id migration, re-scan on display wake, power-state wake via VCP 0xD6, linked brightness), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/powerdisplay — monitor enumeration, brightness/contrast/volume/input-source/color-temperature/power control, DDC/CI + WMI drivers, hot-plug/wake, crash recovery, profiles, LightSwitch, settings. Keywords: PowerDisplay, Power Display, DDC/CI, VCP, WMI brightness, monitor enumeration, EDID, EdidId, multi-monitor, hot-plug, wake, BSOD, crash detection, linked brightness, monitor Id, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys PowerDisplay Knowledge

Grounded engineering knowledge for the PowerToys **Power Display** module — a WinUI 3 flyout
that enumerates connected monitors and adjusts **brightness, contrast, volume, input source,
color temperature, and power state** over **DDC/CI** (external monitors, VCP codes) and **WMI**
(`WmiMonitorBrightness`, laptop/internal panels). This is a **newer, fast-moving module**;
distilled history is limited (~12 merged PRs, one substantial review thread) — entries below are
grounded in source + those PRs/issues. Where the map is thin, verify in source (anti-anchoring).

## When to Use This Skill

- Planning or implementing a change under `src/modules/powerdisplay/` and needing prior art.
- Fixing/triaging a Power Display bug: monitor not detected/duplicated, brightness/contrast not
  applying, wrong values (e.g. Volume Max 255), can't wake a monitor, crash-detected InfoBar,
  BSOD on a specific monitor, settings reset after upgrade, sliders not updating live.
- Reviewing a Power Display PR against maintainer conventions and regression traps.
- Touching DDC/CI or WMI drivers, monitor enumeration/classification, monitor-Id identity, the
  crash-detection/auto-disable lifecycle, hot-plug/wake re-scan, or the flyout ViewModels.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see
anti-anchoring below). Root: `src/modules/powerdisplay/`. Three C# projects plus a C++ module
interface: `PowerDisplay/` (WinUI flyout app), `PowerDisplay.Lib/` (drivers + services),
`PowerDisplay.Models/` (POCOs/serialization), `PowerDisplayModuleInterface/` (runner glue).

| Sub-feature | Implementation (file · symbol) |
|---|---|
| Monitor enumeration / classification (WMI-first, DDC/CI fallback) | `PowerDisplay/Helpers/MonitorManager.cs` (`InitializeControllers`, discovery); routes to controllers by `CommunicationMethod` |
| DDC/CI control (VCP get/set) | `PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs` (`GetBrightnessAsync`/`SetBrightnessAsync`, `Get/SetVcpFeatureAsync`, `SetPowerStateAsync`, `DiscoverMonitorsAsync`) |
| DDC/CI P/Invoke + capability syscalls | `PowerDisplay.Lib/Drivers/DDC/DdcCiNative.cs`, `MonitorDiscoveryHelper.cs`, `PhysicalMonitorHandleManager.cs`; `Drivers/PInvoke.cs`, `NativeConstants.cs` |
| WMI brightness control (internal panels) | `PowerDisplay.Lib/Drivers/WMI/WmiController.cs` |
| VCP codes | `NativeConstants.cs`: Brightness `0x10`, Contrast `0x12`, Volume `0x62`, ColorPreset `0x14`, InputSource `0x60`, PowerMode `0xD6` |
| VCP capabilities parsing (MCCS) | `PowerDisplay.Lib/Utils/MccsCapabilitiesParser.cs`, `VcpNames.cs`, Models `VcpCapabilities.cs`/`VcpFeatureValue.cs`/`MonitorCapabilities.cs` |
| Monitor identity (stable Id from DevicePath / WMI InstanceName; EdidId; legacy) | `PowerDisplay.Lib/Models/MonitorIdentity.cs` (`FromDevicePath`, `FromInstanceName`, `EdidIdFromMonitorId`, `IsLegacyId`, `LegacyEdidId`, `LegacyMonitorNumber`); `PowerDisplay.Models/MonitorIdComparer.cs` |
| Legacy Id migration (`{Source}_{EdidId}_{N}` → DevicePath Id) | `PowerDisplay.Lib/Services/MonitorIdMigrator.cs` |
| Per-monitor settings rebuild / preserve on rediscovery | `PowerDisplay.Lib/Services/MonitorSettingsRebuilder.cs` |
| Built-in monitor blacklist (skip DDC/CI on known-BSOD models by EdidId) | `PowerDisplay.Lib/Services/MonitorBlacklistService.cs` (`IsBlocked`); data `PowerDisplay.Models/BuiltInMonitorBlacklist.{cs,json}`, `MonitorBlacklistEntry.cs` |
| Crash detection scope (writes `discovery.lock` around capability fetch) | `PowerDisplay.Lib/Services/CrashDetectionScope.cs` (`Begin`, `Dispose`, `ProcessExit` safety-net); `IProcessExitHook.cs` |
| Crash recovery (Phase 0 orphan-lock detect → auto-disable) | `PowerDisplay.Lib/Services/CrashRecovery.cs` (`DetectOrphanAndDisable`, `CreateDefault`) |
| Display wake / hot-plug watcher (GUID_CONSOLE_DISPLAY_STATE, WM_DISPLAYCHANGE) | `PowerDisplay/Helpers/DisplayChangeWatcher.cs` (`Start`/`Stop`, PowerSettingRegisterNotification) |
| Display rotation | `PowerDisplay.Lib/Services/DisplayRotationService.cs` |
| Linked ("All Displays") brightness | `PowerDisplay/ViewModels/MainViewModel.LinkedBrightness.cs`; planner `PowerDisplay.Lib/Services/LinkedBrightnessPlanner.cs`; settings `linked_levels_active`, `excluded_from_sync_monitor_ids` |
| Slider commit debounce | `PowerDisplay/Helpers/SliderCommitScheduler.cs`, `SliderExtensions.cs` (`MouseWheelChange`) |
| Flyout ViewModels (monitors, settings, per-monitor) | `PowerDisplay/ViewModels/MainViewModel*.cs`, `MonitorViewModel.cs`, `PowerStateItem.cs`, `InputSourceItem.cs`, `ColorTemperatureItem.cs` |
| Flyout windows / UI | `PowerDisplay/PowerDisplayXAML/MainWindow.xaml(.cs)`, `IdentifyWindow`, `MonitorIcon` |
| Global hotkey | `PowerDisplay/Helpers/HotkeyService.cs` (`Initialize`, `HandleMessage`, `ReloadSettings`) |
| Profiles (per-monitor snapshots) | `PowerDisplay.Models/PowerDisplayProfile(s).cs`, `ProfileHelper.cs`, `PowerDisplay.Lib/Services/ProfileService.cs` |
| LightSwitch theme→profile link | `PowerDisplay/Services/LightSwitchService.cs` (`GetProfileForTheme`) |
| Tray icon | `PowerDisplay/Helpers/TrayIconService.cs` |
| Settings data model (persisted to `settings.json`) | `src/settings-ui/Settings.UI.Library/PowerDisplayProperties.cs`; Settings page `src/settings-ui/Settings.UI/.../PowerDisplayViewModel`/`PowerDisplayPage.xaml` |
| Runner module interface / process lifecycle / IPC events | `PowerDisplayModuleInterface/dllmain.cpp`, `PowerDisplayProcessManager.{cpp,h}`, `Trace.cpp` |

Settings model **defaults matter for compatibility**: old `settings.json` without a new key
deserializes to the constructor default (e.g. `MouseWheelIncrement` → `5`), so no migration is
needed for additive settings (PR #49002).

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### DDC/CI capability-fetch BSOD on non-conformant monitors
- **Symptom:** hard **BSOD `0x139` / STACK_COOKIE_CHECK_FAILURE** in
  `win32kfull!DdcciGetCapabilitiesStringFromMonitor` when a specific monitor is connected/hot-plugged
  (often USB-C→HDMI); reboot loop.
- **Where:** DDC capability syscall in `DdcCiController.DiscoverMonitorsAsync` →
  `GetCapabilitiesStringLength`/`CapabilitiesRequestAndCapabilitiesReply`
  (`MonitorDiscoveryHelper`/`DdcCiNative`); filtered by `MonitorManager` **before** any controller
  dispatch via `MonitorBlacklistService.IsBlocked`.
- **Root cause:** a Windows **kernel** stack-buffer overrun on malformed capabilities strings —
  *not* PowerDisplay's bug, but PowerDisplay is the most common user-mode caller on hot-plug. The
  only user-space mitigation is to **never call** the capabilities API on known-bad models.
- **Guardrail:** add the offending model's **EdidId** (PnP mfr + product code) to
  `BuiltInMonitorBlacklist.json`; keep the `[EdidId=…] [FriendlyName=…] [DevicePath=…]` log line
  emitted immediately before each capabilities syscall so a dump-free triage yields the entry. The
  blacklist match is `OrdinalIgnoreCase`. Evidence:
  [PR #48051](https://github.com/microsoft/PowerToys/pull/48051), issues
  [#47556](https://github.com/microsoft/PowerToys/issues/47556),
  [#47968](https://github.com/microsoft/PowerToys/issues/47968).

### Auto-disable + crash-lock after a real capability-fetch crash
- **Symptom:** after a DDC/CI crash the module should stop retrying; page shows a locked "PowerDisplay
  has crashed" InfoBar until the user dismisses it.
- **Where:** `CrashDetectionScope.Begin` writes `discovery.lock` (WriteThrough + flush-to-disk)
  around Phase 2 capability fetch; on next startup **Phase 0** `CrashRecovery.DetectOrphanAndDisable`
  finds the orphan lock, writes `crash_detected.flag`, sets `enabled.PowerDisplay=false` in global
  `settings.json`, signals `POWER_DISPLAY_AUTO_DISABLE_EVENT`; `PowerDisplayModuleInterface.dll`
  listener calls `disable()`; `PowerDisplayViewModel` binds `IsCrashLockActive`.
- **Guardrail:** the lock must survive any path that can't run user-mode cleanup; deleting the lock
  is the **commit point** of recovery. Don't weaken the flush/ordering. Evidence:
  [PR #47734](https://github.com/microsoft/PowerToys/pull/47734).

### False-positive crash detection on cooperative shutdown
- **Symptom:** "PowerDisplay always shows the crash detected info bar" even though no crash occurred.
- **Where:** cooperative exits — Runner `TerminateApp` NamedPipe, `Terminate` named event, tray-quit,
  Runner-exit detection, upgrades — all call `Environment.Exit(0)` (→ `ExitProcess`), which kills
  threads abruptly so the `using`/`finally` that `Dispose()`s `CrashDetectionScope` never runs and
  `discovery.lock` orphans; Phase 0 then false-positives.
- **Root cause:** `Environment.Exit` skips `try/finally`; the lock was designed to survive only
  *involuntary* kills.
- **Guardrail:** `CrashDetectionScope` registers an `AppDomain.ProcessExit` safety-net that
  best-effort deletes the lock — `ProcessExit` fires for `Environment.Exit` but **not** for
  `FailFast`/BSOD/external `TerminateProcess`, exactly partitioning cooperative vs involuntary exit.
  Preserve that partition. Evidence:
  [PR #48173](https://github.com/microsoft/PowerToys/pull/48173), issue
  [#48169](https://github.com/microsoft/PowerToys/issues/48169).

### Monitor classification: internal (WMI) vs external (DDC/CI)
- **Symptom:** built-in laptop panel "can't detect the display" on dual-GPU/MUX laptops when the
  **discrete GPU** drives it; or an internal panel routed to DDC/CI and dropped.
- **Where:** `MonitorManager` discovery + `WmiController`/`DdcCiController` routing; identity via
  `MonitorIdentity.FromDevicePath`/`FromInstanceName`.
- **Root cause:** classifying by nominal `OutputTechnology` (the discrete GPU reports the eDP panel
  as `DISPLAYPORT_EXTERNAL` 10 instead of `INTERNAL` 0x80000000). A strict "internal→WMI-only,
  external→DDC-only, no fallback" classifier then dropped the panel.
- **Guardrail:** classify by **capability, not nominal type** — run WMI discovery first over the full
  `QueryDisplayConfig` inventory; every display `WmiMonitorBrightness` exposes is WMI-controlled, the
  rest go to DDC/CI. Keep `Monitor.Id` byte-identical to the DDC route so saved settings survive
  upgrades. **Accepted trade-off:** a monitor exposing *both* WMI and DDC is WMI-only (loses
  contrast/volume/input/color/power). Evidence:
  [PR #48637](https://github.com/microsoft/PowerToys/pull/48637), issue
  [#48587](https://github.com/microsoft/PowerToys/issues/48587); earlier classifier from PR #47740.

### Per-monitor settings reset on upgrade (monitor-Id format changes)
- **Symptom:** every upgrade silently resets per-monitor Enable* toggles (input source, color
  temperature, power state) for monitors the user had already opted into.
- **Where:** `MonitorIdMigrator`; consumed where preserved settings are re-applied
  (`MonitorSettingsRebuilder`, `ApplyPreservedUserSettings`).
- **Root cause:** the persisted Id format changed (`{Source}_{EdidId}_{N}` →  DevicePath-based),
  so the direct-Id lookup can never match old `DDC_DELD1A8_1` / `WMI_BOE0900_2` keys.
- **Guardrail:** migrate legacy Ids by matching on **EdidId** before the direct lookup; keep
  `MonitorIdComparer` as the single Id-equality authority. Whenever the Id scheme changes again, add a
  migration path and tests. Evidence:
  [PR #47977](https://github.com/microsoft/PowerToys/pull/47977) (format introduced #47712).

### Woken/hot-plugged monitors not recognized
- **Symptom:** monitors woken from sleep stay unrecognized until manual re-discovery; duplicated
  monitor entries until reboot.
- **Where:** `DisplayChangeWatcher` (subscribes `GUID_CONSOLE_DISPLAY_STATE` via
  `PowerSettingRegisterNotification`) → triggers `MonitorManager` re-scan.
- **Guardrail:** on wake, **lock the UI immediately** to block stale interactions before the re-scan
  completes, then re-scan. Unregister the power notification on dispose. Evidence:
  [PR #47876](https://github.com/microsoft/PowerToys/pull/47876), issue
  [#47951](https://github.com/microsoft/PowerToys/issues/47951); open dup case
  [#48977](https://github.com/microsoft/PowerToys/issues/48977).

### Cancelled discovery is converted into an empty successful result
- **Known current violation:** `SafeDiscoverAsync` catches `Exception`, which also catches
  `OperationCanceledException`, and converts cancellation into an empty successful result. Treat
  cancellation propagation as an existing defect when this path is touched.
- **Symptom:** a cancellation request becomes an empty controller result instead of stopping.
- **Where:** `MonitorManager.DiscoverMonitorsAsync` / `SafeDiscoverAsync`;
  `MainViewModel.InitializeAsync` and `RefreshMonitorsAsync`.
- **Root cause:** `SafeDiscoverAsync` catches every `Exception`, including
  `OperationCanceledException`, and returns an empty collection. WMI and DDC discovery are awaited
  sequentially and independently; there is no parallel all-or-nothing failure model.
- **Guardrail:** keep ordinary controller failures isolated while rethrowing cancellation so the
  existing lifetime token retains cancellation semantics. This does not create per-refresh
  newest-wins behavior; no such generation guard exists. Verify directly in `SafeDiscoverAsync`;
  it is unrelated to the
  monitor-retention work in PR #47712.

### Verify physical monitor handle ownership when changing map updates
- **Classification:** source-derived review heuristic, not a proven current regression or complete
  description of implemented safeguards.
- **Risk:** handle count can grow after repeated rescans, or a VCP operation can fail if ownership
  is duplicated or a still-used handle is destroyed.
- **Where:** `PhysicalMonitorHandleManager.UpdateHandleMap`, `CleanupUnusedHandles`, and `Dispose`.
- **Review heuristic:** trace every returned handle from discovery through map replacement and
  disposal. Verify which handles are reused, newly adopted, rejected, and destroyed; do not assume
  deduplication, zero-handle rejection, or idempotence unless current code/tests prove it.

### Preserve implemented wake and hot-plug rescan safeguards
- **Classification:** implemented invariant, not a demonstrated regression.
- **Where:** `DisplayChangeWatcher.OnDeviceAdded`, `OnDeviceRemoved`,
  `HandleDisplayStateChange`, and `NotifyAndSchedule`.
- **Current behavior:** the watcher ignores events before initial enumeration and routes device/wake
  signals through a cancelling debounce. These safeguards predate PR #47876; that PR added the
  console-display-state wake rescan.
- **Guardrail:** preserve pre-enumeration suppression and the existing debounce when changing wake
  or hot-plug behavior. Do not describe this as a known duplicate-rescan defect or as a per-refresh
  newest-wins generation guard.

### Power state was one-directional (couldn't wake a monitor)
- **Symptom:** Power Display could sleep a monitor (Standby/Suspend/Off) but selecting **On** did
  nothing.
- **Where:** `MonitorViewModel.HandlePowerStateSelectionChanged` early-returned for On
  (`0x01`), so `SetPowerStateAsync` → `DdcCiController.SetPowerStateAsync` →
  `SetVcpFeatureAsync(monitor, 0xD6, value)` never fired.
- **Root cause:** a stale single-monitor assumption ("the monitor must be on to see the UI") survived
  a refactor after multi-monitor + real-power-state-reflection landed.
- **Guardrail:** don't guard power-state writes on an assumed current state; DDC/CI stays reachable in
  Standby/Suspend/Off(DPM) so writing `0x01` wakes it. `Off (Hard)` `0x05` may still need a physical
  wake (cuts the DDC channel). Evidence:
  [PR #48628](https://github.com/microsoft/PowerToys/pull/48628), issue
  [#48428](https://github.com/microsoft/PowerToys/issues/48428); still-open
  [#49048](https://github.com/microsoft/PowerToys/issues/49048).

## Review Rules

Enforce these when reviewing or authoring Power Display changes. Read the diff cold first
(anti-anchoring), then apply only to touched paths.

- **Never send DDC/CI capability requests to a monitor without checking the blacklist first.**
  Route enumeration through `MonitorBlacklistService.IsBlocked` and keep the pre-syscall
  `[EdidId=…]` log line — the kernel BSOD leaves no other trace ([PR #48051](https://github.com/microsoft/PowerToys/pull/48051)).
- **Preserve the cooperative-vs-involuntary exit partition.** `discovery.lock` may only be deleted on
  clean/`ProcessExit` paths, never blindly on startup; deleting it is the recovery commit point
  ([PR #48173](https://github.com/microsoft/PowerToys/pull/48173), [PR #47734](https://github.com/microsoft/PowerToys/pull/47734)).
- **Classify monitors by capability, not by `OutputTechnology`.** WMI-first, DDC/CI for the rest; the
  nominal connector type is unreliable on hybrid-GPU laptops ([PR #48637](https://github.com/microsoft/PowerToys/pull/48637)).
- **Keep `Monitor.Id` stable and route all Id equality through `MonitorIdComparer`.** Any change to
  the Id scheme needs a `MonitorIdMigrator` path + tests, or per-monitor settings silently reset on
  upgrade ([PR #47977](https://github.com/microsoft/PowerToys/pull/47977)).
- **Additive settings must default to today's behavior in the model constructor** so old
  `settings.json` deserializes unchanged (e.g. `MouseWheelIncrement` default `5`); no migration for
  additive keys ([PR #49002](https://github.com/microsoft/PowerToys/pull/49002)).
- **Prefer the shared debounce (`SliderCommitScheduler.Schedule`) over per-feature timers.** Linked
  brightness duplicated debounce logic and was consolidated in review
  ([PR #48207](https://github.com/microsoft/PowerToys/pull/48207)).
- **Discovery is partial-failure tolerant, but cancellation is currently swallowed.** Preserve
  successful results from other controllers and rethrow `OperationCanceledException` to preserve
  the existing lifetime token's semantics; do not claim per-refresh cancellation/newest-wins.
- **Audit physical-monitor handle ownership when map logic changes.** Trace adoption, reuse, and
  destruction against current code/tests rather than assuming safeguards are implemented.
- **Coalesce display-change signals.** Ignore initial watcher enumeration and debounce wake/hot-plug
  notifications before starting a refresh. These safeguards predate PR #47876; that PR added the
  console-display-state wake signal.
- **Identity migrations cover every side file.** Changes to `Monitor.Id` must update settings,
  `profiles.json`, and `monitor_state.json`, not only the in-memory monitor list (#47977).
- **Don't over-engineer seed/selection logic.** Maintainer pushed back on a complex initial-brightness
  planner; the accepted rule is "lowest Windows DISPLAY number, Id ordering as fallback"
  ([PR #48207](https://github.com/microsoft/PowerToys/pull/48207)).
- **All models participate in source-generated JSON contexts (AOT).** New serialized types/properties
  must be registered in the relevant `JsonSerializerContext` (e.g. `ProfileSerializationContext`,
  `MonitorBlacklistSerializationContext`, `JsonSourceGenerationContext`), or serialization fails at
  runtime.
- **New end-user strings go in `Strings/en-us/Resources.resw` and are surfaced via `x:Uid`.** Run
  `.\.pipelines\applyXamlStyling.ps1 -Main` before pushing XAML — CI enforces XAML styling
  ([PR #48207](https://github.com/microsoft/PowerToys/pull/48207) review).
- **UI hangs → lock the UI, don't hide latency behind inner flags.** Reviewer's rule: if the UI may
  hang during work (e.g. re-scan), lock it rather than add hidden suppression logic
  ([PR #48207](https://github.com/microsoft/PowerToys/pull/48207)).

## Pitfalls

- **The BSOD is a Windows kernel defect, not a PowerDisplay bug** — you can only *avoid* the API on
  known-bad models via the EdidId blacklist; you cannot "fix" the crash in this module.
- **`discovery.lock` is a crash sentinel, not a mutex.** Its *presence at startup* means "the last
  capability fetch didn't clean up" → treated as a crash. Cooperative exits rely on the
  `AppDomain.ProcessExit` hook to delete it; `Environment.Exit` alone skips `try/finally`.
- **A monitor exposing both WMI brightness and DDC/CI is intentionally WMI-only** and will *not*
  offer contrast/volume/input-source/color-temperature/power. This is a deliberate trade-off from
  PR #48637 — don't "fix" it by re-adding an OutputTechnology classifier.
- **`Off (Hard)` (VCP `0xD6`=`0x05`) can cut the DDC command channel** — the monitor may then need a
  physical button/HDMI-CEC wake; only Standby/Suspend/Off(DPM) reliably wake via `0x01`.
- **Enabling/restoring linked brightness must NOT change hardware brightness by itself.** The first
  hardware write happens only after the user moves the master slider; a suppress flag exists solely
  to stop the seed assignment being treated as a user change (PR #48207 thread). Don't remove it
  without reproducing the "both monitors jump to the seed value" behavior.
- **Monitor `Id` derives from `DevicePath`/WMI `InstanceName`, not friendly name.** Friendly names
  duplicate across identical models; only `MonitorIdentity`/`MonitorIdComparer` give stable identity.
- **VCP value scaling is per-monitor.** Brightness/volume are percentages of a monitor-specific max
  (`monitor.BrightnessVcpMax`); monitors reporting non-standard maxima (e.g. Volume Max = 255) need
  the reported max respected, not a hardcoded 100 (open issue [#49120](https://github.com/microsoft/PowerToys/issues/49120)).
- **Power Display is a separate `PowerDisplay.exe` process** launched by the runner via
  `PowerDisplayModuleInterface.dll`; enable/disable and crash-auto-disable sync through named events
  and global `settings.json`, not in-process calls.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Power Display PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/powerdisplay/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/powerdisplay)
- [DDC/CI & MCCS (VESA)](https://en.wikipedia.org/wiki/Display_Data_Channel) · [Monitor Configuration API](https://learn.microsoft.com/en-us/windows/win32/monitor/monitor-configuration-functions) · [WmiMonitorBrightness](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightness) · [RegisterPowerSettingNotification / GUID_CONSOLE_DISPLAY_STATE](https://learn.microsoft.com/en-us/windows/win32/power/power-setting-guids)
