# LightSwitch PR Review Checklist

Apply after reading the diff cold (see anti-anchoring note in SKILL.md). Only check rows whose
files/areas the PR actually touches.

## Scheduling & time math
- [ ] Angle/time normalization uses real modulo (`fmod` / `% 1440` with the `+1440` guard), **not**
      single-pass `if (x>360) x-=360;` (`ThemeScheduler.cpp`, `LightSwitchUtils.h`). (#46957)
- [ ] `CalculateSunriseSunset` polar sentinel (`cosH` out of range → `-1`) is handled, not fed into
      `toLocal` as a real time. (#46954)
- [ ] `ShouldBeLight` still covers both the normal (`light<dark`) and wrap-around cases; new call
      sites route through it rather than re-deriving boundaries. (#45723, #45860)
- [ ] Coordinate validation does not reject a real `(0,0)`; "unset" is represented separately. (#46955)

## Theme application (registry)
- [ ] Reads/writes of `SystemUsesLightTheme` / `AppsUseLightTheme` keep **1 = light, 0 = dark**.
- [ ] `changeSystem` and `changeApps` handled independently; no accidental coupling.
- [ ] `WM_SETTINGCHANGE("ImmersiveColorSet")` + `WM_THEMECHANGED` broadcast after any theme write.
- [ ] `ColorPrevalence` reset on light-mode switch is preserved/understood if `ThemeHelper.cpp` changes.

## Settings & observers
- [ ] Every new persisted setting has a `SettingId` (`SettingsConstants.h`) **and** a
      `NotifyObservers` call in `LoadSettings`. (#46956)
- [ ] Settings reload path still debounces (`InitFileWatcher`); no assumption of instant reload.

## Service & inter-module
- [ ] GPO gate (`getConfiguredLightSwitchEnabledValue`) honored before starting the service.
- [ ] Service launch/teardown correct; self-terminates when parent PID exits (`--pid`).
- [ ] `NotifyPowerDisplay` fires on **every** override/apply, not only on `false→true` entry. (#47190)
- [ ] Direction-specific named events used so consumers don't read a half-written registry. (#47190)
- [ ] Manual-override sticky/clear logic (boundary crossing, midnight wrap) unchanged or tested.
- [ ] New/changed logging is size-bounded (long-running service). (#48212)

## Platform & hygiene
- [ ] No undocumented internal Windows APIs (wallpaper feature was reverted for this). (#44588)
- [ ] `IsNightLightEnabled` blob-offset parsing changes are justified and defensive.
- [ ] Project files use `$(RepoRoot)`, deps via `Directory.Packages.props`. (#44639)
- [ ] End-user strings localizable; UI-test coverage under `src/modules/LightSwitch/Tests/LightSwitch.UITests/` where relevant.

## Tests
- [ ] A test accompanies the fix (unit or `LightSwitch.UITests`), especially for schedule boundaries,
      wrap-around, offsets, and startup theme application.
