# Power Display — Regression Catalog & Key Decisions

Progressive-disclosure companion to `SKILL.md`. Grounded in merged PRs + linked issues from the
raw distillation set (`raw/PowerDisplay/`). **Newer module — history is thin; verify in source.**

## Merged PRs (chronological)

| PR | Title | Durable lesson |
|---|---|---|
| [#47875](https://github.com/microsoft/PowerToys/pull/47875) | Max compatibility mode setting | Opt-in DDC discovery of monitors that don't advertise capabilities; toggling raises `RescanPowerDisplayMonitorsEvent`; hide brightness slider when VCP `0x10` absent. |
| [#47876](https://github.com/microsoft/PowerToys/pull/47876) | Re-scan monitors on display wake | Subscribe `GUID_CONSOLE_DISPLAY_STATE`; **lock UI first, then re-scan** so stale interactions are blocked. (#47951) |
| [#47734](https://github.com/microsoft/PowerToys/pull/47734) | Auto-disable on DDC/CI capability-fetch crash | `discovery.lock` sentinel + Phase 0 recovery → `crash_detected.flag`, `enabled.PowerDisplay=false`, `POWER_DISPLAY_AUTO_DISABLE_EVENT`, page lock via `IsCrashLockActive`. (#47556) |
| [#47977](https://github.com/microsoft/PowerToys/pull/47977) | Migrate legacy `{Source}_{EdidId}_{N}` Ids | Match legacy Ids by **EdidId** onto DevicePath Ids or upgrades silently reset Enable* toggles. (Id format from #47712) |
| [#48026](https://github.com/microsoft/PowerToys/pull/48026) | Close flyout on Escape | Flyout had no keyboard close path; handle Escape on RootGrid, matching other PowerToys flyouts. (#48016) |
| [#48051](https://github.com/microsoft/PowerToys/pull/48051) | Built-in monitor blacklist (DDC/CI BSOD) | Skip known-BSOD models by **EdidId** before any capability syscall; embedded JSON data; log EdidId pre-syscall. (#47556, #47968) |
| [#48173](https://github.com/microsoft/PowerToys/pull/48173) | Fix false-positive crash detection | `AppDomain.ProcessExit` safety-net deletes lock on cooperative `Environment.Exit`; not on FailFast/BSOD/TerminateProcess. (#48169) |
| [#48207](https://github.com/microsoft/PowerToys/pull/48207) | Linked brightness control | One "All Displays" master slider; seed = lowest Windows DISPLAY number (Id fallback); suppress flag so enabling linked mode doesn't write hardware; per-monitor exclusions by Id; profiles turn linked off before applying. (#47319) |
| [#48628](https://github.com/microsoft/PowerToys/pull/48628) | Wake monitor via power-state On | Remove UI-layer guard that skipped On (`0x01`); DDC/CI reachable in Standby/Suspend/Off(DPM). (#48428) |
| [#48637](https://github.com/microsoft/PowerToys/pull/48637) | Detect built-in panel driven by discrete GPU | Classify by **capability** (WMI-first), not `OutputTechnology`; deleted `DisplayClassifier`/`IsInternal`; both-WMI+DDC monitors are WMI-only (trade-off). (#48587) |
| [#48915](https://github.com/microsoft/PowerToys/pull/48915) | Refactor transparent overlay | `TransparentWindow` (host) + `TransientSurface` (self-animating acrylic); shared `Common.UI.Controls`. Not PowerDisplay-specific but consumed by flyouts. |
| [#49002](https://github.com/microsoft/PowerToys/pull/49002) | Configurable mouse-wheel increment | Additive `mouse_wheel_increment` (default 5) — old settings deserialize unchanged, no migration; binds all four flyout sliders. (#48805) |

## Key decisions (from review threads)

- **Simplicity over cleverness (PR #48207, reviewer `moooyo`).** Rejected a multi-factor
  initial-brightness planner and extra helper/suppression paths: "using the lowest monitor number
  would be sufficient." Result: seed = lowest Windows DISPLAY number, Id ordering fallback.
- **Consolidate debounce (PR #48207).** Duplicated slider-commit timers were unified into
  `SliderCommitScheduler.Schedule`; linked brightness and per-monitor sliders both use it.
- **The suppress flag stays (PR #48207).** `_suppressLinkedBrightnessBroadcast` prevents the seed
  assignment from being treated as a user brightness change (so enabling linked mode doesn't push all
  monitors to one value). Kept after the author explained the concrete UX failure.
- **UI-only info banner removed (PR #48207, `niels9001`).** Linked-state guidance moved from a
  separate info banner into the link-icon tooltip to keep the multi-monitor panel compact.
- **Lock the UI for latency, don't hide it (PR #48207).** "If we expect the UI will hang for a
  period of time, we need to lock the UI rather than add inner logic."
- **Accepted trade-off (PR #48637).** A monitor exposing both WMI brightness and DDC/CI is WMI-only —
  deliberately removes the whole `OutputTechnology`-misclassification bug class at the cost of
  DDC-only features on such (uncommon) monitors.

## Open issue clusters (as of distillation) — detection reliability

Many open reports center on **monitors not detected / not controllable**, indicating the durable
risk area is discovery/classification and DDC/CI reachability, not the UI:
- Not detected / partially detected: [#49045](https://github.com/microsoft/PowerToys/issues/49045),
  [#48998](https://github.com/microsoft/PowerToys/issues/48998),
  [#48898](https://github.com/microsoft/PowerToys/issues/48898),
  [#48472](https://github.com/microsoft/PowerToys/issues/48472),
  [#48520](https://github.com/microsoft/PowerToys/issues/48520),
  [#48179](https://github.com/microsoft/PowerToys/issues/48179) (USB dock),
  [#48086](https://github.com/microsoft/PowerToys/issues/48086).
- HDR mode: [#49032](https://github.com/microsoft/PowerToys/issues/49032).
- Duplicated entry until reboot: [#48977](https://github.com/microsoft/PowerToys/issues/48977).
- Wrong VCP scaling (Volume Max 255): [#49120](https://github.com/microsoft/PowerToys/issues/49120).
- Power/wake: [#49048](https://github.com/microsoft/PowerToys/issues/49048).
- Slider stale vs external change: [#48888](https://github.com/microsoft/PowerToys/issues/48888).
- Rotation out of sync: [#49098](https://github.com/microsoft/PowerToys/issues/49098).

## Excluded as noise (not distilled)

`/azp run`, `@check-spelling-bot`, "agree" CLA bot lines, XAML-styling nits, and generic
"Something went wrong / sending logs" issues with no diagnosable content
([#48420](https://github.com/microsoft/PowerToys/issues/48420),
[#48034](https://github.com/microsoft/PowerToys/issues/48034),
[#48004](https://github.com/microsoft/PowerToys/issues/48004),
[#48388](https://github.com/microsoft/PowerToys/issues/48388)) — no durable lesson.
