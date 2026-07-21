# LightSwitch Regression & Decision Catalog

Progressive-disclosure companion to `SKILL.md`. LightSwitch is a **young module** (first shipped
~PowerToys 0.98–0.100); history is thin and many reports are still open or not yet assessed. Where an entry
rests only on an issue **title** (issue bodies were unavailable at distill time), it is marked
*(title-only — confirm in source)*.

## Key decisions (from PR/issue history)

- **Wallpaper switching reverted for using undocumented internal Windows APIs.**
  [PR #44588](https://github.com/microsoft/PowerToys/pull/44588) reverted the "switch desktop
  wallpapers with Light/Dark mode" feature. Maintainer rationale (vanzue, MEMBER): the internal APIs
  are "undocumented and come with no compatibility guarantees … in a Microsoft project like PowerToys
  [this] could be misleading, as it may implicitly signal that this is a supported or stable
  approach." Durable lesson: **prefer documented registry keys / public APIs**; internal-API
  features are release blockers. (Re-worked without internal APIs in follow-ups.)

- **Startup must decide *and* apply, not just cache state.**
  [PR #45304](https://github.com/microsoft/PowerToys/pull/45304) (closes
  [#45291](https://github.com/microsoft/PowerToys/issues/45291)) split the overloaded init function
  into `SyncInitialThemeState()` which syncs cached system/apps/Night-Light state **and** calls
  `EvaluateAndApplyIfNeeded` so the correct theme is applied at startup. Also removed an unnecessary
  `OnTick` parameter.

- **Inter-module notifications use direction-specific named events.**
  [PR #47190](https://github.com/microsoft/PowerToys/pull/47190) fixed the LightSwitch↔PowerDisplay
  integration: (1) restored the "Apply monitor settings" Settings UI that had been commented out in
  #46160; (2) made `NotifyPowerDisplay` fire on *every* hotkey override (was gated on
  `isManualOverride` `false→true`, dropping every even press). Uses separate
  `LIGHT_SWITCH_LIGHT_THEME_EVENT` / `LIGHT_SWITCH_DARK_THEME_EVENT` (in
  `src/common/interop/shared_constants.h`) so PowerDisplay never reads a half-written registry.

- **Repo-wide build conventions apply here too.** Project references use `$(RepoRoot)` and deps go
  through `Directory.Packages.props` ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639));
  the module rode .NET 10 ([#41280](https://github.com/microsoft/PowerToys/pull/41280)), VS 2026
  ([#44304](https://github.com/microsoft/PowerToys/pull/44304)), CppWinRT
  ([#45420](https://github.com/microsoft/PowerToys/pull/45420)), and spdlog→vcpkg
  ([#48039](https://github.com/microsoft/PowerToys/pull/48039)) migrations.

## Source-grounded code bugs (precise, verifiable in tree)

These four are grounded directly in current source, not just issue text:

1. **`ThemeScheduler.cpp` single-pass angle normalization** — `L` and `RA` use
   `if (x<0) x+=360; if (x>360) x-=360;`, which cannot correct an overshoot greater than 360°. Use
   `fmod`. ([#46957](https://github.com/microsoft/PowerToys/issues/46957))
2. **Polar sentinel unhandled** — `CalculateSunriseSunset` returns `-1` when `cosH` is out of
   `[-1,1]` (sun never rises/sets), but `toLocal` treats `-1` as a real UT hour.
   ([#46954](https://github.com/microsoft/PowerToys/issues/46954))
3. **`CoordinatesAreValid` rejects real `(0,0)`** — `!(latVal == 0 && lonVal == 0)` overloads the
   equator/prime-meridian point as an "unset" sentinel.
   ([#46955](https://github.com/microsoft/PowerToys/issues/46955))
4. **`SettingId` enum missing profile entries** — it ends at `ChangeApps`; the profile fields
   (`enableDarkModeProfile`, `enableLightModeProfile`, `darkModeProfile`, `lightModeProfile`) are
   read in `LoadSettings` but never `NotifyObservers`, so live edits don't propagate.
   ([#46956](https://github.com/microsoft/PowerToys/issues/46956))

## Open issue index (triage aid — mostly title-only)

Scheduling / correctness:
- [#45723](https://github.com/microsoft/PowerToys/issues/45723) schedule works in reverse *(title-only)*
- [#45860](https://github.com/microsoft/PowerToys/issues/45860) "position check bug" *(title-only)*
- [#45291](https://github.com/microsoft/PowerToys/issues/45291) light returns after restart w/ Follow Night Light (fixed PR#45304)

Theme reverts / default-on:
- [#47566](https://github.com/microsoft/PowerToys/issues/47566) reverts to scheduled after update even if set manually *(title-only)*
- [#46159](https://github.com/microsoft/PowerToys/issues/46159) auto-switches dark→light *(title-only)*
- [#44619](https://github.com/microsoft/PowerToys/issues/44619) light turns on after restart *(title-only)*
- [#48537](https://github.com/microsoft/PowerToys/issues/48537) install switches to light system-wide *(title-only)*
- [#45781](https://github.com/microsoft/PowerToys/issues/45781) / [#45562](https://github.com/microsoft/PowerToys/issues/45562) unexpected theme change *(title-only)*
- [#45044](https://github.com/microsoft/PowerToys/issues/45044) / [#44652](https://github.com/microsoft/PowerToys/issues/44652) should be off by default *(title-only)*

Half-switched / repaint:
- [#48257](https://github.com/microsoft/PowerToys/issues/48257) only changes Windows, not apps *(title-only)*
- [#48082](https://github.com/microsoft/PowerToys/issues/48082) Task Manager mixed light/dark *(title-only)*
- [#48692](https://github.com/microsoft/PowerToys/issues/48692) taskbar thumbnail theme alternates *(title-only)*
- [#46374](https://github.com/microsoft/PowerToys/issues/46374) window elements become invisible *(title-only)*

Service / ops:
- [#48212](https://github.com/microsoft/PowerToys/issues/48212) service log grows to GB, no rotation *(title-only)*
- [#45434](https://github.com/microsoft/PowerToys/issues/45434) doesn't work when PowerToys in background *(title-only)*
- [#46072](https://github.com/microsoft/PowerToys/issues/46072) no effect under admin account *(title-only)*
- [#45142](https://github.com/microsoft/PowerToys/issues/45142) do the checkup on Windows start *(title-only)*

PowerDisplay integration:
- [#48774](https://github.com/microsoft/PowerToys/issues/48774) profile switch not triggering on wake from sleep *(title-only)*
- [#47354](https://github.com/microsoft/PowerToys/issues/47354) hide profile comboboxes when no PowerDisplay profiles *(title-only)*

Other:
- [#49310](https://github.com/microsoft/PowerToys/issues/49310) crash opening shortcut editor while changing system theme *(title-only)*
- [#49110](https://github.com/microsoft/PowerToys/issues/49110) wallpaper switch (dup; feature reverted #44588)

## Excluded as noise (not distilled)
- Build/deps/infra PRs with no LightSwitch-specific lesson (#48039 spdlog→vcpkg, #41280 .NET 10,
  #47119 check-spelling, #45420 CppWinRT, #44304 VS2026, #42642 PowerDisplay intro except its shared
  events, #44795 Awake).
- Pure nitpick review comments (e.g. trailing-whitespace suggestion on #44588).
- Non-English/duplicate/triage-only reports with no actionable detail.
