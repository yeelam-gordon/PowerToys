# LightSwitch Bug Triage: Symptom → Likely File/Function

Map the reported symptom to a starting point, then **confirm in source** (many entries rest on bare
issue titles; the Module Map is a hypothesis, not ground truth).

| Symptom | Start here | Notes / evidence |
|---|---|---|
| Theme reverts to light/scheduled after reboot or PT update | `LightSwitchStateManager.cpp::SyncInitialThemeState` → `EvaluateAndApplyIfNeeded` | Startup must re-apply. #45291/PR#45304; #47566 open; #46159/#44619 closed completed |
| Schedule runs in reverse / wrong at midnight | `LightSwitchUtils.h::ShouldBeLight`; boundary math in `EvaluateAndApplyIfNeeded` | Check both cases + `[0,1439]` normalize. #45723 |
| Wrong/never switching times at high latitude | `ThemeScheduler.cpp::CalculateSunriseSunset` (`cosH` polar `-1`) | Known current violation: callers feed the sentinel to `toLocal`. #46954 |
| Sun mode unusable at (0,0) | `LightSwitchStateManager.cpp::CoordinatesAreValid` | Don't reject real (0,0). #46955 |
| Only system OR only apps theme changes | `LightSwitchService.cpp::ApplyTheme`; `changeSystem`/`changeApps` | Independent toggles. #48257, #48082, #48692 |
| PowerDisplay profile applied every other hotkey press | `LightSwitchStateManager.cpp::OnManualOverride` → `NotifyPowerDisplay` | Notify on every press. PR#47190 |
| PowerDisplay profile does not switch after wake | wake/resume evaluation, current theme state, and `NotifyPowerDisplay` path | Manual switching works in #48774; reproduce the resume-trigger path rather than assuming a registry race. |
| Profile setting change has no live effect | settings watcher → `LightSwitchSettings::LoadSettings` → service settings-event handler → `LightSwitchStateManager::OnSettingsChanged` | Reproduce the full reload path; missing `SettingId` is not established as the cause. #46956 |
| Detect location spins or never completes | `LightSwitchPage.xaml.cs::GetGeoLocation_Click`, `GeoLocationTimeout` | #45860 / PR #45887 timeout and availability fix |
| LightSwitch doesn't run in background | `dllmain.cpp::enable` (`CreateProcessW`, `SearchPathW`); GPO gate | Service launch/locate failure. #45434 |
| Theme switched unexpectedly on install | default `ScheduleMode`/`changeSystem`/`changeApps`; settings.json | Default-on behavior. #48537, #44619, #45781, #45562 |
| Manual override doesn't stick / clears too soon | `EvaluateAndApplyIfNeeded` override branch (boundary crossing, midnight wrap) | #47566 |
| Night Light toggle not detected | `NightLightRegistryObserver.h`; `IsNightLightEnabled` blob bytes 23–24 | Watcher only runs in Follow-Night-Light mode |
| Follow Night Light applies reversed / at startup | `EvaluateAndApplyIfNeeded` (`shouldBeLight = !isNightLightActive`); `SyncInitialThemeState` | #45291 |
| Service log grows to GB | `LightSwitchService` logging / logger config | Add size rotation. #48212 |
| Wallpaper not switching with theme | (feature removed) | Reverted for undocumented-API use. #44588, #49110 |
| Elements invisible / mixed theme in some apps | app-side theme repaint after `WM_THEMECHANGED` broadcast | Broadcast is best-effort. #46374, #48082 |

## Triage steps
1. Reproduce and note: which `ScheduleMode`? `changeSystem`/`changeApps`? coordinates? Night Light on?
2. Read `settings.json` for LightSwitch to get the *effective* config (not struct defaults).
3. Confirm the service process is running (it's separate; launched with `--pid`).
4. Localize via the table, **verify the hypothesis in source**, then reason from the actual code.
