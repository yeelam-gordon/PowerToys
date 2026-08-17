# Power Display — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split note:** `SKILL.md` owns the operational regression playbooks, review rules, and
> guardrails. This companion retains the historical evidence, source anchors, reviewer decisions,
> chronology, unresolved issue clusters, and caveats without repeating those instructions.

The module is new and its history is thin. Treat every source anchor as a lead to verify against the
current tree.

## Change evidence (chronological)

| Sequence | Evidence | Source anchors | Decision or regression recorded |
|---|---|---|---|
| 1 | [PR #47734](https://github.com/microsoft/PowerToys/pull/47734), issue [#47556](https://github.com/microsoft/PowerToys/issues/47556) | `CrashDetectionScope`; `CrashRecovery.DetectOrphanAndDisable`; global `settings.json`; `POWER_DISPLAY_AUTO_DISABLE_EVENT`; `PowerDisplayViewModel.IsCrashLockActive` | Capability discovery gained the `discovery.lock` sentinel, Phase 0 orphan recovery, auto-disable, runner notification, and settings-page crash lock. |
| 2 | Id format introduced in PR #47712 | `MonitorIdentity`; persisted per-monitor settings | Monitor identity moved from legacy `{Source}_{EdidId}_{N}` values toward DevicePath-derived IDs. |
| 3 | Earlier classifier in PR #47740 | `MonitorManager`; `DisplayClassifier`/`IsInternal` (later removed) | The initial internal/external split relied on nominal display classification. |
| 4 | [PR #47875](https://github.com/microsoft/PowerToys/pull/47875) | compatibility setting; `RescanPowerDisplayMonitorsEvent`; VCP `0x10` capability | Maximum-compatibility mode made broader DDC discovery opt-in and hid brightness when unsupported. |
| 5 | [PR #47876](https://github.com/microsoft/PowerToys/pull/47876), issue [#47951](https://github.com/microsoft/PowerToys/issues/47951) | `DisplayChangeWatcher`; `GUID_CONSOLE_DISPLAY_STATE` | Display wake triggers a rescan; the UI is locked before discovery so stale controls cannot be used. |
| 6 | [PR #47977](https://github.com/microsoft/PowerToys/pull/47977) | `MonitorIdMigrator`; `MonitorSettingsRebuilder`; `MonitorIdentity.LegacyEdidId`; `MonitorIdComparer` | Legacy IDs are reconciled to DevicePath IDs by EdidId so upgrades preserve Enable* settings. |
| 7 | [PR #48026](https://github.com/microsoft/PowerToys/pull/48026), issue [#48016](https://github.com/microsoft/PowerToys/issues/48016) | flyout `RootGrid` key handling | Escape became the keyboard close path, matching other PowerToys flyouts. |
| 8 | [PR #48051](https://github.com/microsoft/PowerToys/pull/48051), issues [#47556](https://github.com/microsoft/PowerToys/issues/47556), [#47968](https://github.com/microsoft/PowerToys/issues/47968) | `MonitorBlacklistService.IsBlocked`; `BuiltInMonitorBlacklist.json`; pre-capability-call EdidId logging | Known kernel-crashing monitor models are skipped by EdidId before any DDC/CI capability syscall. |
| 9 | [PR #48173](https://github.com/microsoft/PowerToys/pull/48173), issue [#48169](https://github.com/microsoft/PowerToys/issues/48169) | `CrashDetectionScope`; `AppDomain.ProcessExit` | Cooperative `Environment.Exit` cleanup was separated from FailFast, BSOD, and external termination to prevent false crash detection. |
| 10 | [PR #48207](https://github.com/microsoft/PowerToys/pull/48207), issue [#47319](https://github.com/microsoft/PowerToys/issues/47319) | `MainViewModel.LinkedBrightness`; `LinkedBrightnessPlanner`; `SliderCommitScheduler`; `linked_levels_active`; `excluded_from_sync_monitor_ids` | Linked brightness added a master slider, per-monitor exclusions, lowest-DISPLAY-number seeding, suppressed initial broadcast, shared debounce, and profile-driven unlinking. |
| 11 | [PR #48628](https://github.com/microsoft/PowerToys/pull/48628), issue [#48428](https://github.com/microsoft/PowerToys/issues/48428) | `MonitorViewModel.HandlePowerStateSelectionChanged`; `DdcCiController.SetPowerStateAsync`; VCP `0xD6` | The UI-layer rejection of power-state On (`0x01`) was removed, allowing wake over reachable DDC/CI states. |
| 12 | [PR #48637](https://github.com/microsoft/PowerToys/pull/48637), issue [#48587](https://github.com/microsoft/PowerToys/issues/48587) | `MonitorManager`; `WmiController`; `DdcCiController`; deleted `DisplayClassifier`/`IsInternal` | Classification changed from `OutputTechnology` to WMI capability first, with DDC/CI used for the remaining monitors. |
| 13 | [PR #48915](https://github.com/microsoft/PowerToys/pull/48915) | shared `Common.UI.Controls.TransparentWindow`; `TransientSurface` | Flyouts adopted the shared host/self-animating acrylic split; this evidence is cross-module rather than Power Display-specific. |
| 14 | [PR #49002](https://github.com/microsoft/PowerToys/pull/49002), issue [#48805](https://github.com/microsoft/PowerToys/issues/48805) | `PowerDisplayProperties.MouseWheelIncrement`; four flyout sliders | Additive `mouse_wheel_increment` defaults to 5, preserving old settings without migration. |

## Current-source invariants and violations

| Source anchor | Classification | Verified current-source observation |
|---|---|---|
| `MonitorManager.SafeDiscoverAsync` | Known current violation | The broad `Exception` catch also converts `OperationCanceledException` into an empty successful result. Rethrowing would preserve the existing lifetime token's semantics; current source has no per-refresh newest-wins generation guard. |
| `PhysicalMonitorHandleManager.UpdateHandleMap`, `CleanupUnusedHandles`, `Dispose` | Verification heuristic | Trace adoption, reuse, and destruction whenever map logic changes. This ledger does not claim that deduplication, zero-handle rejection, or idempotence are fully implemented. |
| `DisplayChangeWatcher.OnDeviceAdded`, `OnDeviceRemoved`, `HandleDisplayStateChange`, `NotifyAndSchedule` | Implemented invariant | Pre-enumeration suppression and cancelling debounce existed with the watcher; PR #47876 added console-display-state wake rescan. Preserve the safeguards without claiming a known duplicate-rescan defect or newest-wins generation guard. |

## Reviewer decision ledger

| Evidence | Reviewer decision | Result retained in code |
|---|---|---|
| PR #48207, reviewer `moooyo` | Prefer the lowest Windows DISPLAY number over a multi-factor initial-brightness planner. | Lowest DISPLAY number seeds linked brightness; Id ordering is the fallback. |
| PR #48207 | Consolidate duplicate slider commit timers. | Linked and per-monitor sliders use `SliderCommitScheduler.Schedule`. |
| PR #48207 | Keep `_suppressLinkedBrightnessBroadcast` after the author demonstrated that seed assignment otherwise writes brightness to every monitor. | Enabling/restoring linked mode does not itself change hardware brightness. |
| PR #48207, reviewer `niels9001` | Remove the separate linked-state information banner. | Guidance lives in the link-icon tooltip to keep the panel compact. |
| PR #48207 | If latency can hang the UI, lock the UI rather than hide it behind inner flags. | Discovery/rescan paths expose a locked state. |
| PR #48637 | Accept WMI-only routing when a monitor exposes both WMI brightness and DDC/CI. | The `OutputTechnology` misclassification class is removed at the cost of DDC-only controls on uncommon dual-capability monitors. |

## Unresolved issue clusters (at distillation time)

- **Not detected or only partly detected:** [#49045](https://github.com/microsoft/PowerToys/issues/49045),
  [#48998](https://github.com/microsoft/PowerToys/issues/48998),
  [#48898](https://github.com/microsoft/PowerToys/issues/48898),
  [#48472](https://github.com/microsoft/PowerToys/issues/48472),
  [#48520](https://github.com/microsoft/PowerToys/issues/48520),
  [#48179](https://github.com/microsoft/PowerToys/issues/48179) (USB dock), and
  [#48086](https://github.com/microsoft/PowerToys/issues/48086).
- **Mode/topology variants:** HDR [#49032](https://github.com/microsoft/PowerToys/issues/49032);
  duplicated entry until reboot [#48977](https://github.com/microsoft/PowerToys/issues/48977).
- **State/value synchronization:** non-100 VCP scaling
  [#49120](https://github.com/microsoft/PowerToys/issues/49120); stale slider after external change
  [#48888](https://github.com/microsoft/PowerToys/issues/48888); rotation drift
  [#49098](https://github.com/microsoft/PowerToys/issues/49098).
- **Power/wake:** [#49048](https://github.com/microsoft/PowerToys/issues/49048).

The concentration of open reports around discovery, classification, and DDC/CI reachability is
evidence of the dominant residual risk; it is not proof that the UI is defect-free.

## Caveats and excluded noise

- The DDC capability BSOD is a Windows kernel failure. The recorded module response is avoidance by
  known EdidId plus diagnostic logging, not a kernel fix.
- WMI-first routing deliberately withholds contrast, volume, input, color, and power controls from
  monitors that expose both WMI and DDC/CI.
- VCP maxima are monitor-specific; issue #49120 shows why a hardcoded 100 is unsafe.
- `Off (Hard)` may sever DDC/CI and require a physical wake even though other low-power states accept
  VCP On.
- Excluded as non-decision evidence: `/azp run`, check-spelling and CLA bot chatter, XAML styling
  nits, and undiagnosed “Something went wrong / sending logs” reports
  [#48420](https://github.com/microsoft/PowerToys/issues/48420),
  [#48034](https://github.com/microsoft/PowerToys/issues/48034),
  [#48004](https://github.com/microsoft/PowerToys/issues/48004), and
  [#48388](https://github.com/microsoft/PowerToys/issues/48388).
