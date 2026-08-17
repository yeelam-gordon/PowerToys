# LightSwitch — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

This file owns historical evidence, chronology, review decisions, open issue clusters, and
confidence caveats. `SKILL.md` owns the current module map, review rules, and operational guidance.
LightSwitch is young (first shipped around PowerToys 0.98–0.100), so title-only reports remain
unverified symptoms.

## Decision evidence

### LS-D1 — Undocumented wallpaper APIs were release-blocking

- **Decision:** Revert wallpaper switching implemented with undocumented internal Windows APIs;
  prefer documented registry keys and public APIs.
- **Review rationale:** @vanzue noted that internal APIs have no compatibility guarantee and their
  use in a Microsoft project could misleadingly imply support or stability.
- **Chronology/evidence:** Feature implementation → revert
  [PR #44588](https://github.com/microsoft/PowerToys/pull/44588) → unsuccessful rework attempt
  [PR #44598](https://github.com/microsoft/PowerToys/pull/44598) (closed without merge).
  [#49110](https://github.com/microsoft/PowerToys/issues/49110) is a later closed duplicate request;
  current source has no wallpaper-switching implementation.

### LS-D2 — Startup synchronizes state and applies the decision

- **Source:** `LightSwitchStateManager.cpp`, `SyncInitialThemeState`,
  `EvaluateAndApplyIfNeeded`.
- **Finding:** Initialization cached system/apps/Night-Light state without necessarily applying the
  schedule.
- **Decision:** Split the overloaded initialization path; startup must sync state and immediately
  evaluate/apply. Remove the unused `OnTick` parameter.
- **Chronology/evidence:** [issue #45291](https://github.com/microsoft/PowerToys/issues/45291) →
  [PR #45304](https://github.com/microsoft/PowerToys/pull/45304).

### LS-D3 — PowerDisplay notifications carry direction and fire every override

- **Source:** `LightSwitchStateManager.cpp`, `NotifyPowerDisplayThemeChanged`, `OnManualOverride`;
  `src/common/interop/shared_constants.h`.
- **Findings:** PR #47190 found notification gated on `isManualOverride` false→true, dropping every
  even hotkey press. Direction-specific events already existed from PowerDisplay integration.
- **Decision:** Synchronize cached state and notify on every hotkey override. Preserve
  `LIGHT_SWITCH_LIGHT_THEME_EVENT` / `LIGHT_SWITCH_DARK_THEME_EVENT` as the existing contract.
- **Additional accepted change:** Restore the “Apply monitor settings” UI commented out in #46160.
- **Chronology/evidence:** direction events [PR #42642](https://github.com/microsoft/PowerToys/pull/42642);
  every-other-override fix [PR #47190](https://github.com/microsoft/PowerToys/pull/47190).

### LS-D4 — Detect-location operations are bounded

- **Source:** `LightSwitchPage.xaml.cs`, `GetGeoLocation_Click`, `GeoLocationTimeout`;
  `LightSwitchPage.xaml`, `LocationErrorText`.
- **Finding:** `Geolocator.GetGeopositionAsync()` could wait indefinitely. The click handler already
  requested permission and required `Allowed`; the missing protection was a bounded wait, while an
  earlier dialog-open availability pre-check was added separately.
- **Decision:** Add the availability pre-check, apply a 10-second cancellation timeout, show an
  error, and retain manual coordinate entry.
- **Chronology/evidence:** [issue #45860](https://github.com/microsoft/PowerToys/issues/45860) →
  [PR #45887](https://github.com/microsoft/PowerToys/pull/45887).

## Source-grounded findings

| ID | Source/symbol | Finding | Decision status / evidence interpretation | Evidence |
|---|---|---|---|---|
| LS-E2 | `ThemeScheduler.cpp`, `CalculateSunriseSunset`, `toLocal` | `cosH` outside `[-1,1]` returns `-1`, and current callers pass it to `toLocal` as a real UT hour. | Known current violation verified in source; issue closed completed April 19, 2026. | [#46954](https://github.com/microsoft/PowerToys/issues/46954) |
| LS-E3 | `LightSwitchStateManager.cpp`, `CoordinatesAreValid` | `!(latVal == 0 && lonVal == 0)` rejects the real equator/prime-meridian coordinate. | Open issue; the evidence distinguishes a real `(0,0)` coordinate from an unset-value representation. | [#46955](https://github.com/microsoft/PowerToys/issues/46955) |
| LS-E4 | `LightSwitchSettings.cpp`, `LoadSettings`; settings-event handler in `LightSwitchService.cpp`; `LightSwitchStateManager.cpp`, `OnSettingsChanged` | Profile-setting refresh report. Current source handles the settings event, reloads the full settings object, and calls the state manager, so missing per-field `SettingId` entries are not a supported root cause. | Open symptom evidence; reproduce the complete watcher/service/state-manager path before assigning causality. | [#46956](https://github.com/microsoft/PowerToys/issues/46956) |
| LS-E5 | `LightSwitchUtils.h`, `ShouldBeLight`; `LightSwitchStateManager.cpp`, `EvaluateAndApplyIfNeeded`, `DetectAndHandleExternalThemeChange` | Reports describe reversed scheduling or the wrong side of a midnight boundary. | Symptom evidence; normal/wrap-around boundary handling is the investigation area, but the root cause is unverified. | [#45723](https://github.com/microsoft/PowerToys/issues/45723) |
| LS-E6 | `LightSwitchService.cpp`, `ApplyTheme`; `EvaluateAndApplyIfNeeded` | System and apps themes are independently gated by `changeSystem` / `changeApps`; mixed output may be configuration rather than failure. | Symptom evidence; no single repaint/apply defect is established because the two configuration axes can intentionally differ. | [#48257](https://github.com/microsoft/PowerToys/issues/48257), [#48082](https://github.com/microsoft/PowerToys/issues/48082), [#48692](https://github.com/microsoft/PowerToys/issues/48692) |

## Evidence clusters (lifecycle noted)

Unless a fix is named, these entries preserve symptoms and must be confirmed in source.

- [#46957](https://github.com/microsoft/PowerToys/issues/46957) questions one-pass angle
  normalization, but valid longitude/date inputs keep the calculated values within one adjustment.
  No reachable module defect or durable fix decision is established.

| Cluster | Reports and chronology |
|---|---|
| Scheduling/correctness | [#45723](https://github.com/microsoft/PowerToys/issues/45723) reverse-schedule symptom; [#45860](https://github.com/microsoft/PowerToys/issues/45860) geolocation hang → fixed by [PR #45887](https://github.com/microsoft/PowerToys/pull/45887); [#45291](https://github.com/microsoft/PowerToys/issues/45291) restart/Follow Night Light → fixed by [PR #45304](https://github.com/microsoft/PowerToys/pull/45304) |
| Revert/default-on | [#47566](https://github.com/microsoft/PowerToys/issues/47566) returns to schedule after update (open when distilled); [#46159](https://github.com/microsoft/PowerToys/issues/46159) dark→light and [#44619](https://github.com/microsoft/PowerToys/issues/44619) light after restart (closed completed when distilled); [#48537](https://github.com/microsoft/PowerToys/issues/48537) install switches system-wide; [#45781](https://github.com/microsoft/PowerToys/issues/45781), [#45562](https://github.com/microsoft/PowerToys/issues/45562) unexpected changes; [#45044](https://github.com/microsoft/PowerToys/issues/45044), [#44652](https://github.com/microsoft/PowerToys/issues/44652) should be off by default |
| Half-switched/repaint | [#48257](https://github.com/microsoft/PowerToys/issues/48257) Windows but not apps; [#48082](https://github.com/microsoft/PowerToys/issues/48082) mixed Task Manager; [#48692](https://github.com/microsoft/PowerToys/issues/48692) alternating taskbar thumbnails; [#46374](https://github.com/microsoft/PowerToys/issues/46374) invisible window elements |
| Service/operations | [#48212](https://github.com/microsoft/PowerToys/issues/48212) unbounded GB-scale log; [#45434](https://github.com/microsoft/PowerToys/issues/45434) background failure; [#46072](https://github.com/microsoft/PowerToys/issues/46072) no effect under admin; [#45142](https://github.com/microsoft/PowerToys/issues/45142) startup check |
| PowerDisplay | [#48774](https://github.com/microsoft/PowerToys/issues/48774) wake-from-sleep profile switch; [#47354](https://github.com/microsoft/PowerToys/issues/47354) hide profile boxes without profiles |
| Other open reports | None retained in this row. |

Resolved history: shortcut-editor theme crash
[#49310](https://github.com/microsoft/PowerToys/issues/49310) was fixed by
[PR #49334](https://github.com/microsoft/PowerToys/pull/49334); wallpaper request
[#49110](https://github.com/microsoft/PowerToys/issues/49110) closed completed after the reverted and
unmerged work described in LS-D1.

## Exclusion decisions

- Omit build/dependency/infra PRs with no LightSwitch-specific lesson:
  [#44639](https://github.com/microsoft/PowerToys/pull/44639),
  [#48039](https://github.com/microsoft/PowerToys/pull/48039),
  [#41280](https://github.com/microsoft/PowerToys/pull/41280), #47119,
  [#45420](https://github.com/microsoft/PowerToys/pull/45420),
  [#44304](https://github.com/microsoft/PowerToys/pull/44304), #42642 except shared events, and
  #44795.
- Omit pure nitpick review comments such as trailing whitespace on #44588.
- Omit non-English, duplicate, or triage-only reports without actionable detail, except where a
  duplicate is relevant to chronology.

## Caveats

- Issue-body evidence establishes reported symptoms, not a root cause or current status.
- Source-grounded findings were precise when distilled but may move or be fixed; verify the current
  tree and issue state.
- `SystemUsesLightTheme` and `AppsUseLightTheme` are separate axes, so mixed themes can be
  configuration rather than a defect.
