# LightSwitch Bug Triage: Symptom → Likely File/Function

Map the reported symptom to a starting point, then **confirm in source** (many entries rest on bare
issue titles; the Module Map is a hypothesis, not ground truth).

| Symptom | Start here | Notes / evidence |
|---|---|---|
| Theme reverts to light/scheduled after reboot or PT update | `LightSwitchStateManager.cpp::SyncInitialThemeState` → `EvaluateAndApplyIfNeeded` | Startup must re-apply. #45291/PR#45304; open #47566, #46159, #44619 |
| Schedule runs in reverse / wrong at midnight | `LightSwitchUtils.h::ShouldBeLight`; boundary math in `EvaluateAndApplyIfNeeded` | Check both cases + `[0,1439]` normalize. #45723, #45860 |
| Wrong/never switching times at high latitude | `ThemeScheduler.cpp::CalculateSunriseSunset` (`cosH` polar `-1`) | #46954 |
| Sun times drift / L normalization wrong | `ThemeScheduler.cpp` (`L`/`RA` single-pass ±360) | Use `fmod`. #46957 |
| Sun mode unusable at (0,0) | `LightSwitchStateManager.cpp::CoordinatesAreValid` | Don't reject real (0,0). #46955 |
| Only system OR only apps theme changes | `LightSwitchService.cpp::ApplyTheme`; `changeSystem`/`changeApps` | Independent toggles. #48257, #48082, #48692 |
| PowerDisplay profile applied every other hotkey press | `LightSwitchStateManager.cpp::OnManualOverride` → `NotifyPowerDisplay` | Notify on every press. PR#47190 |
| PowerDisplay applies wrong profile (race) | `NotifyPowerDisplay`; `LIGHT_SWITCH_*_THEME_EVENT` | Direction-specific events. PR#47190; #48774 |
| Profile setting change has no live effect | `LightSwitchSettings.cpp::LoadSettings` vs `SettingsConstants.h SettingId` | Missing SettingId/NotifyObservers. #46956 |
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
