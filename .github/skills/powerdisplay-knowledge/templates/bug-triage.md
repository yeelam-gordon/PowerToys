# Power Display — Bug Triage (symptom → likely file/function)

Use the Module Map as **hypotheses to confirm in source**, not ground truth. If a symptom
doesn't map cleanly, reason from the symptom and verify — the history here is thin.

| Symptom | Start here | Notes |
|---|---|---|
| BSOD when a specific monitor is connected/hot-plugged | `MonitorBlacklistService.IsBlocked`, `BuiltInMonitorBlacklist.json`; DDC capability syscall in `DdcCiController.DiscoverMonitorsAsync` | Kernel `win32kfull` overrun; mitigate by adding the EdidId. (#47556/#47968, PR #48051) |
| "PowerDisplay has crashed" InfoBar with no real crash | `CrashDetectionScope` (ProcessExit hook), `CrashRecovery.DetectOrphanAndDisable` | Cooperative `Environment.Exit` orphaning `discovery.lock`. (#48169, PR #48173) |
| Built-in laptop panel not detected (dual-GPU/MUX laptop) | `MonitorManager` classification, `WmiController`, `MonitorIdentity.FromInstanceName` | Classify by capability, not `OutputTechnology`. (#48587, PR #48637) |
| External monitor not detected / detected via dock | `MonitorManager` discovery, `DdcCiController`; check blacklist + max-compat mode | Many open reports; confirm DDC/CI reachable. (#48179, #48472, #48898) |
| Monitor duplicated until reboot | `DisplayChangeWatcher` re-scan, `MonitorIdComparer` identity | Hot-plug/wake de-dup. (#48977) |
| Woken monitor stays unrecognized | `DisplayChangeWatcher` (GUID_CONSOLE_DISPLAY_STATE) → `MonitorManager` re-scan | Lock UI, then re-scan. (#47951, PR #47876) |
| Can't wake monitor / Turn Off(DPM) doesn't work | `MonitorViewModel.HandlePowerStateSelectionChanged` → `DdcCiController.SetPowerStateAsync` (VCP 0xD6) | On=0x01 wakes; Off(Hard)=0x05 may cut DDC. (#48428/#49048, PR #48628) |
| Per-monitor toggles reset after upgrade | `MonitorIdMigrator`, `MonitorSettingsRebuilder` | Legacy Id → DevicePath Id migration by EdidId. (PR #47977) |
| Wrong brightness/volume values (e.g. Volume Max 255) | `VcpFeatureValue` scaling, `DdcCiController.Get/SetVcpFeatureAsync`, `MccsCapabilitiesParser` | Respect per-monitor VCP max. (#49120) |
| Brightness slider stale vs external change | `MainViewModel` load / settings-updated IPC; per-monitor `MonitorViewModel` refresh | Live-update path. (#48888) |
| Brightness slider missing on a monitor | capability gate (VCP 0x10 present); max-compatibility-mode setting | Slider hidden when no 0x10. (PR #47875) |
| ESC doesn't close flyout | `MainWindow.xaml.cs` KeyDown / RootGrid Escape | (#48016, PR #48026) |
| Tray icon never appears at startup | `TrayIconService`, module launch in `PowerDisplayModuleInterface` | (#48295) |
| LightSwitch profile not applied on wake | `LightSwitchService.GetProfileForTheme`; `ProfileStore`/`ProfileHelper`; `MainViewModel.ApplyProfileAsync`; wake path | (#48774) |
| Linked "All Displays" slider jumps monitors on enable | `MainViewModel.LinkedBrightness.cs` suppress flag, `LinkedBrightnessPlanner` | Seed must not write hardware. (PR #48207) |
| Rotation out of sync with system | `DisplayRotationService` | (#49098) |
