---
name: powertoys-lightswitch-knowledge
description: 'PowerToys LightSwitch module knowledge: feature->file/function map, regression playbooks (sunrise/sunset math, coordinate validation, schedule wrap-around, startup theme sync, manual-override hotkey toggling, PowerDisplay profile events, Personalize registry theme keys, Night Light registry observer), maintainer review rules and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/LightSwitch — scheduled light/dark theme switching, sunset/sunrise scheduling, geolocation, Follow Night Light, fixed hours, hotkey toggle, Windows service, settings. Keywords: LightSwitch, Light Switch, dark mode, light mode, theme scheduler, sunrise sunset, geolocation, Night Light, AppsUseLightTheme, SystemUsesLightTheme, Personalize registry, PowerDisplay, Windows service, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys LightSwitch Knowledge

Grounded engineering knowledge for the PowerToys **LightSwitch** module — a background Windows
service that automatically flips Windows between **light and dark themes** on a schedule (fixed
hours, computed sunset→sunrise from geolocation, or Follow Night Light), plus a global hotkey to
toggle manually. Use it to localize code fast, avoid known regression traps, and enforce the
conventions maintainers already established.

> **This is a young module** (first shipped ~PowerToys 0.98–0.100). History is comparatively thin
> and many bug reports are still open or not yet assessed. Where evidence is a bare issue title, this file
> says so — treat those as *symptoms to confirm in source*, not settled root causes.

## When to Use This Skill

- Planning or implementing a change under `src/modules/LightSwitch/` and needing prior art.
- Fixing/triaging a LightSwitch bug: theme not switching, switching in reverse, wrong times, theme
  reverting after restart/update, apps theme not changing while system does, manual override not
  sticking, PowerDisplay profile not applied on toggle.
- Reviewing a LightSwitch PR and checking it against maintainer conventions and regression traps.
- Touching the sunrise/sunset math, coordinate validation, schedule wrap-around, the Windows
  service worker loop, the settings file-watcher/debounce, or the Night Light registry observer.
- Adding a new `ScheduleMode`, a new setting, or new PowerDisplay/inter-module event integration.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).
Source root: `src/modules/LightSwitch/`.

| Sub-feature | Implementation (file · function) |
|---|---|
| Module entry / Settings page glue / hotkey registration | `src/modules/LightSwitch/LightSwitchModuleInterface/dllmain.cpp` `LightSwitchInterface` (`enable`, `disable`, `get_hotkeys`, `on_hotkey`, `parse_hotkey`, `set_config`) |
| Launch the background service | `dllmain.cpp::enable` — `CreateProcessW` for `PowerToys.LightSwitchService.exe --pid <pid>` (built from `src/modules/LightSwitch/LightSwitchService/LightSwitchService.vcxproj`) |
| Manual toggle hotkey (default Win+Ctrl+Shift+D) | `dllmain.cpp` `m_toggle_theme_hotkey`, `on_hotkey`, `LightSwitchInterface::ToggleTheme` |
| Manual-override signaling to service | named event `POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE` (`dllmain.cpp` `ToggleTheme` `SetEvent`; consumed in service worker loop) |
| GPO enable/disable gate | `LightSwitchService.cpp::wWinMain` via `powertoys_gpo::getConfiguredLightSwitchEnabledValue()`; policy `ConfigureEnabledUtilityLightSwitch` (`src/common/utils/gpo.h`) |
| Windows service host / worker loop / minute tick | `src/modules/LightSwitch/LightSwitchService/LightSwitchService.cpp` `_tmain`, `ServiceMain`, `ServiceWorkerThread` (waits on stop/parent/override/settings events + per-minute timeout) |
| External-theme-change detection (user flips theme in Settings) | `LightSwitchService.cpp::DetectAndHandleExternalThemeChange` → triggers `OnManualOverride` |
| Apply theme to registry (the actual switch) | `LightSwitchService.cpp::ApplyTheme` → `SetSystemTheme` / `SetAppsTheme` |
| Theme registry read/write (Personalize keys) | `src/modules/LightSwitch/LightSwitchLib/ThemeHelper.cpp` `SetSystemTheme`, `SetAppsTheme`, `GetCurrentSystemTheme`, `GetCurrentAppsTheme`, `ResetColorPrevalence` |
| Night Light detection (read) | `ThemeHelper.cpp::IsNightLightEnabled` (parses `Data` blob bytes 23/24 under CloudStore bluelightreduction key) |
| Night Light change watcher | `src/modules/LightSwitch/LightSwitchService/NightLightRegistryObserver.h` (`RegNotifyChangeKeyValue` thread; header-only impl) |
| Decide/apply theme (the brain) | `src/modules/LightSwitch/LightSwitchService/LightSwitchStateManager.cpp` `EvaluateAndApplyIfNeeded`, `OnTick`, `OnSettingsChanged`, `OnManualOverride`, `OnNightLightChange`, `SyncInitialThemeState` |
| Runtime-only state (not persisted) | `LightSwitchStateManager.h` `struct LightSwitchState` |
| Schedule decision (is it light-time now?) | `src/modules/LightSwitch/LightSwitchService/LightSwitchUtils.h` `ShouldBeLight` (handles midnight wrap), `GetNowMinutes` |
| Sunrise/sunset computation | `src/modules/LightSwitch/LightSwitchService/ThemeScheduler.cpp` `CalculateSunriseSunset` (+ `deg2rad`/`rad2deg` in `.h`) |
| Coordinate validation | `LightSwitchStateManager.cpp::CoordinatesAreValid`; sun-time recompute in `update_sun_times` |
| Settings load / schema / defaults | `src/modules/LightSwitch/LightSwitchService/LightSwitchSettings.cpp` `LoadSettings`; `src/modules/LightSwitch/LightSwitchService/LightSwitchSettings.h` `struct LightSwitchConfig`, `enum class ScheduleMode`, `FromString`/`ToString` |
| Settings file-watcher + debounce (3 s) | `LightSwitchSettings.cpp::InitFileWatcher` (`FileWatcher` + `std::jthread` debounce → `m_settingsChangedEvent`) |
| Settings observer registration IDs | `src/modules/LightSwitch/LightSwitchService/SettingsConstants.h` `enum class SettingId`; `SettingsObserver.h` |
| PowerDisplay profile notify (light/dark events) | `LightSwitchStateManager.cpp::NotifyPowerDisplay` → `CommonSharedConstants::LIGHT_SWITCH_LIGHT_THEME_EVENT` / `..._DARK_THEME_EVENT` |
| Telemetry | `src/modules/LightSwitch/LightSwitchModuleInterface/trace.cpp` (`ShortcutInvoked`, `Enable`); `src/modules/LightSwitch/LightSwitchService/trace.cpp` (`ScheduleModeToggled`, `ThemeTargetChanged`) |
| Settings UI view-model (C#) | `src/settings-ui/Settings.UI/ViewModels/LightSwitchViewModel.cs` (+ page/XAML) |
| UI tests | `src/modules/LightSwitch/Tests/LightSwitch.UITests/` (`TestGeolocation`, `TestOffset`, `TestShortcut`, `TestUpdateManualTime`, `TestUserSelectedLocation`) |

**Two theme axes, two registry values.** LightSwitch controls **System** theme
(`SystemUsesLightTheme`) and **Apps** theme (`AppsUseLightTheme`) *independently*, gated by
`changeSystem` / `changeApps`. Both live under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` (`PERSONALIZATION_REGISTRY_PATH`).
After writing, LightSwitch broadcasts `WM_SETTINGCHANGE("ImmersiveColorSet")` + `WM_THEMECHANGED`
so running apps repaint. Value semantics: **1 = light, 0 = dark** (see `GetCurrentSystemTheme`).

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog + open reports in
[references/regression-catalog.md](./references/regression-catalog.md).

### Theme reverts on Windows restart / after PowerToys update
- **Symptom:** after reboot (or PT update) the theme snaps back to light/scheduled even though the
  user had it dark, notably with Follow Night Light.
- **Where:** `LightSwitchStateManager.cpp::SyncInitialThemeState` (called once from
  `ServiceWorkerThread` at startup); schedule eval in `EvaluateAndApplyIfNeeded`.
- **Root cause:** startup did not re-evaluate + apply the correct theme against current settings; a
  single init function was overloaded for two purposes.
- **Guardrail:** on startup, sync cached system/apps/Night-Light state **and** call
  `EvaluateAndApplyIfNeeded` so the correct theme is applied immediately. Don't reuse one function
  for "read state" and "decide+apply". Evidence: issue
  [#45291](https://github.com/microsoft/PowerToys/issues/45291) fixed by
  [PR #45304](https://github.com/microsoft/PowerToys/pull/45304); related open reports
  [#47566](https://github.com/microsoft/PowerToys/issues/47566),
  [#46159](https://github.com/microsoft/PowerToys/issues/46159),
  [#44619](https://github.com/microsoft/PowerToys/issues/44619) (confirm in source; issue bodies unavailable).

### PowerDisplay profile only applied on every *other* hotkey press
- **Symptom:** binding a monitor profile per theme via PowerDisplay, the profile only follows the
  theme on odd-numbered hotkey presses; even presses flip the theme but leave the profile stuck.
- **Where:** `LightSwitchStateManager.cpp::OnManualOverride` → `NotifyPowerDisplay`.
- **Root cause:** the notify was gated on `isManualOverride` transitioning `false→true`; the toggle
  flips the bool every press, so every second call skipped the notify.
- **Guardrail:** ModuleInterface flips the Windows theme *before* signaling; sync cached state and
  call `NotifyPowerDisplay` on **every** override, not only when "entering" override. Evidence:
  [PR #47190](https://github.com/microsoft/PowerToys/pull/47190) (see the in-code comment in
  `OnManualOverride`).

### PowerDisplay reads registry before LightSwitch finished writing (race)
- **Symptom:** wrong monitor profile applied because PowerDisplay read `AppsUseLightTheme` mid-write.
- **Where:** `NotifyPowerDisplay` event names; `CommonSharedConstants` in `src/common/interop/shared_constants.h`.
- **Root cause:** a single "theme changed" event forced the consumer to re-read the registry.
- **Guardrail:** signal **separate** light vs dark named events (`LIGHT_SWITCH_LIGHT_THEME_EVENT` /
  `LIGHT_SWITCH_DARK_THEME_EVENT`) that carry the direction, so PowerDisplay never has to read the
  half-written registry. Evidence: [PR #47190](https://github.com/microsoft/PowerToys/pull/47190)
  (comment in `NotifyPowerDisplay`).

### Schedule runs in reverse / wrong side of the boundary
- **Symptom:** dark mode active during the day and light at night — the schedule is inverted; or the
  wrap-around window is mishandled around midnight.
- **Where:** `LightSwitchUtils.h::ShouldBeLight`; boundary math in `EvaluateAndApplyIfNeeded`;
  `DetectAndHandleExternalThemeChange`.
- **Root cause:** off-by-one / inverted comparison in the light-vs-dark window, especially the
  wrap-around case where light starts in the evening and dark in the morning.
- **Guardrail:** keep the two `ShouldBeLight` cases explicit (normal `light<dark`, and wrap-around)
  and normalize all three inputs into `[0,1439]`; add a unit/UI test per boundary and per wrap
  direction. Evidence: [#45723](https://github.com/microsoft/PowerToys/issues/45723) ("works in
  reverse"), [#45860](https://github.com/microsoft/PowerToys/issues/45860) ("position check bug")
  — confirm in source; issue bodies unavailable.

### Sunrise/sunset math wrong for polar / edge coordinates
- **Symptom:** garbage or never-switching times at high latitudes (sun never rises/sets); or the L
  (ecliptic longitude) value wraps incorrectly at year/day extremes.
- **Where:** `ThemeScheduler.cpp::CalculateSunriseSunset` — `cosH > 1 || cosH < -1` returns `-1`
  (then treated as a real time by `toLocal`); `L`/`RA` clamped with single `if (L<0) L+=360; if
  (L>360) L-=360;` instead of a true modulo (`fmod`).
- **Root cause:** single-pass ±360 normalization can't wrap values that overshoot by >360; the
  polar sentinel `-1` is not handled by callers.
- **Guardrail:** use `fmod`-style modulo for angle normalization; propagate/handle the polar
  "no sunrise/sunset" sentinel instead of feeding `-1` into `toLocal`. Evidence:
  [#46957](https://github.com/microsoft/PowerToys/issues/46957) (single-pass vs fmod),
  [#46954](https://github.com/microsoft/PowerToys/issues/46954) (polar garbage) — grounded in
  `ThemeScheduler.cpp`.

### `(0,0)` rejected as invalid coordinate
- **Symptom:** a user legitimately on the equator/prime-meridian (Gulf of Guinea) can't use Sun mode.
- **Where:** `LightSwitchStateManager.cpp::CoordinatesAreValid` — `!(latVal == 0 && lonVal == 0)`.
- **Root cause:** `(0,0)` is used as a sentinel for "unset", colliding with a real location.
- **Guardrail:** don't overload `(0,0)` as "unset"; track configured-vs-unset explicitly (e.g. an
  empty/absent string, not the value). Evidence:
  [#46955](https://github.com/microsoft/PowerToys/issues/46955) — grounded in source.

### Changing a PowerDisplay profile setting has no effect until restart
- **Symptom:** editing dark/light profile bindings in Settings doesn't take effect live.
- **Where:** `LightSwitchSettings.cpp::LoadSettings` (profile fields) vs `SettingsConstants.h`
  `enum class SettingId`.
- **Root cause:** `SettingId` stops at `ChangeApps` — it has **no** entries for
  `enableDarkModeProfile` / `enableLightModeProfile` / `darkModeProfile` / `lightModeProfile`, so
  those branches update the struct but never `NotifyObservers`.
- **Guardrail:** when adding a persisted setting that observers must react to, add a `SettingId`
  **and** call `NotifyObservers` in `LoadSettings`. Evidence:
  [#46956](https://github.com/microsoft/PowerToys/issues/46956) — grounded in source.

### Apps theme unchanged while System theme changes (or vice-versa)
- **Symptom:** Windows shell flips but apps stay on the old theme (mixed light/dark: Task Manager,
  taskbar thumbnails).
- **Where:** `ApplyTheme` (gated by `s.changeApps` / `s.changeSystem`); `EvaluateAndApplyIfNeeded`
  computes `appsNeedsToChange` / `systemNeedsToChange` independently.
- **Root cause:** the two axes are independent toggles; if only one is enabled, the other never moves
  (by design) — but users perceive it as a bug.
- **Guardrail:** when triaging "half switched", first check `changeApps`/`changeSystem`; keep the two
  axes consistent in UI copy. Evidence:
  [#48257](https://github.com/microsoft/PowerToys/issues/48257),
  [#48082](https://github.com/microsoft/PowerToys/issues/48082),
  [#48692](https://github.com/microsoft/PowerToys/issues/48692) (confirm in source; bodies unavailable).

## Review Rules

Enforce these when reviewing or authoring LightSwitch changes:

- **Every new persisted setting needs a `SettingId` + `NotifyObservers`.** Adding a field to
  `LightSwitchConfig` and reading it in `LoadSettings` is not enough — without a `SettingId`
  (`SettingsConstants.h`) and a `NotifyObservers` call, live updates silently do nothing (#46956).
- **Normalize angles with real modulo, not single-pass ±360.** In `ThemeScheduler.cpp`, a lone
  `if (x>360) x-=360;` can't correct overshoots >360; use `fmod` and handle the polar `cosH` out-of-
  range sentinel (#46957, #46954).
- **Never overload `(0,0)` (or any valid value) as an "unset" sentinel** — it rejects a real
  location; represent unset separately (#46955).
- **Notify inter-module consumers on every state change, not only on entry.** `NotifyPowerDisplay`
  must fire on every override/apply; direction-specific named events avoid registry read races
  ([PR #47190](https://github.com/microsoft/PowerToys/pull/47190)).
- **Do the schedule decision through `ShouldBeLight` and keep both wrap cases + `[0,1439]`
  normalization** — don't re-implement boundary math ad hoc in new call sites (#45723, #45860).
- **Do not depend on undocumented internal Windows APIs.** The wallpaper-switching feature was
  reverted precisely because it used undocumented internal APIs with no compatibility guarantee —
  in a Microsoft project that implicitly signals an unsupported approach as blessed. Prefer
  documented registry keys / public APIs
  ([revert PR #44588](https://github.com/microsoft/PowerToys/pull/44588) discussion).
- **Respect the GPO gate.** New entry points must honor
  `powertoys_gpo::getConfiguredLightSwitchEnabledValue()` before starting the service
  (`LightSwitchService.cpp::wWinMain`).
- **Broadcast `WM_SETTINGCHANGE`/`WM_THEMECHANGED` after any registry theme write** so apps repaint;
  don't write `Personalize` values silently (`ThemeHelper.cpp`).
- **Bound service logging.** The service is long-running; there is an open report of the log growing
  to multiple GB with no rotation — verify size rotation for any new logging
  ([#48212](https://github.com/microsoft/PowerToys/issues/48212)).
- **No bare relative paths in project files.** Use `$(RepoRoot)`; add deps via
  `Directory.Packages.props` (repo-wide convention,
  [PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).

## Pitfalls

- **Never** read `Personalize` values as bool without remembering **1 = light, 0 = dark**
  (`GetCurrentSystemTheme` returns `value == 1`). Inverting this silently reverses the whole module.
- **System theme ≠ Apps theme.** They are two independent registry values and two independent
  toggles (`changeSystem`/`changeApps`). "Half the UI switched" is usually one toggle off, not a bug.
- **Switching to light mode also resets `ColorPrevalence`.** `SetSystemTheme(true)` calls
  `ResetColorPrevalence()` (writes `ColorPrevalence=0`) — a deliberate side effect; don't "clean it
  up" without understanding accent-on-title-bars behavior (`ThemeHelper.cpp`).
- **`IsNightLightEnabled` parses a raw binary blob** at fixed offsets (bytes 23–24 == `0x10 0x00`)
  under an undocumented CloudStore key. It is inherently fragile to Windows changes — treat any
  Night-Light regression as "the blob format moved" first (`ThemeHelper.cpp`).
- **The service is a separate process, launched by the module** via `CreateProcessW` with `--pid`;
  it self-terminates when the parent PowerToys PID exits. A "LightSwitch doesn't run in background"
  report is often the service failing to launch/locate the exe, not the schedule logic
  (`dllmain.cpp::enable`, [#45434](https://github.com/microsoft/PowerToys/issues/45434)).
- **Settings reloads are debounced ~3 s.** `InitFileWatcher` waits for the file to stabilize before
  firing `m_settingsChangedEvent`; don't expect instant reaction to a settings write in tests.
- **Manual override is sticky until a scheduled boundary is crossed.** `EvaluateAndApplyIfNeeded`
  clears `isManualOverride` only when `now` crosses the light/dark boundary (with midnight wrap) —
  a manual toggle intentionally survives ticks until then.
- **`OnTick` skips evaluation in Follow-Night-Light mode** (`lastAppliedMode == FollowNightLight`);
  in that mode the `NightLightRegistryObserver` drives changes, not the minute tick.
- **Default schedule mode disagrees across layers.** `dllmain.cpp` `ModuleSettings` and
  `LightSwitchConfig` differ (`Off` vs `FixedHours`, `changeSystem/Apps` true vs false). Confirm the
  effective default from settings.json, not from one struct — several "switched my theme on install"
  reports trace to default-on behavior ([#48537](https://github.com/microsoft/PowerToys/issues/48537),
  [#44619](https://github.com/microsoft/PowerToys/issues/44619)).

## Using This Skill in PR Review (Anti-Anchoring)

**Read the diff cold first.** Do not skim this file's playbooks and then hunt the diff for those
themes — that anchors you on recurring concerns and lowers your catch rate on the PR's actual issues.

1. Read the diff and form your own list of concerns from what actually changed.
2. **Then** cross-check the touched files against the Module Map, Regression Playbooks, and Review
   Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat this file as a checklist for the touched area, not a script for the whole review.

Because this module is young and several entries rest on **bare issue titles** (bodies were not
available at distill time), be extra skeptical: if a symptom doesn't map cleanly to a row above,
reason from the symptom and verify in source rather than force-fitting the map.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions + open-issue index.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a LightSwitch PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/LightSwitch/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/LightSwitch)
- [Personalize theme keys / dark mode](https://learn.microsoft.com/windows/apps/desktop/modernize/apply-windows-themes) ·
  [Sunrise/sunset algorithm (NOAA/Almanac)](https://en.wikipedia.org/wiki/Sunrise_equation) ·
  [WM_SETTINGCHANGE](https://learn.microsoft.com/windows/win32/winmsg/wm-settingchange)
