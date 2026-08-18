# Power Display — PR Review Checklist

Apply to any PR touching `src/modules/powerdisplay/` (or `src/settings-ui/**/PowerDisplay*`).
**Read the diff cold first**, form your own concerns, then use this only for the touched areas.

## Drivers / DDC/CI + WMI
- [ ] Any new path that calls DDC/CI **capability** APIs goes through `MonitorBlacklistService.IsBlocked` first, and preserves the pre-syscall `[EdidId=…] [FriendlyName=…] [DevicePath=…]` log line. (BSOD `0x139`, PR #48051)
- [ ] Monitor classification is by **capability** (WMI-first, DDC/CI for the rest), never by nominal `OutputTechnology`. (PR #48637)
- [ ] VCP values respect the **per-monitor** max (e.g. `BrightnessVcpMax`), not a hardcoded 100/percentage assumption. (issue #49120)
- [ ] Power-state writes are not guarded on an assumed "current" state; wake via VCP `0xD6`=`0x01` is reachable. (PR #48628)

## Monitor identity / settings compatibility
- [ ] All Id equality goes through `MonitorIdComparer`; `Monitor.Id` stays byte-identical to prior releases.
- [ ] Any change to the Id scheme ships a `MonitorIdMigrator` path + tests (else per-monitor toggles reset on upgrade). (PR #47977)
- [ ] Additive `settings.json` keys default to today's behavior in the model constructor; no migration needed. (PR #49002)

## Crash detection / lifecycle
- [ ] `discovery.lock` is only deleted on clean / `AppDomain.ProcessExit` paths, never blindly at startup. (PR #48173)
- [ ] Cooperative-vs-involuntary exit partition intact (`ProcessExit` fires for `Environment.Exit`, not for `FailFast`/BSOD/`TerminateProcess`).
- [ ] Recovery commit point (lock delete) still ordered after `crash_detected.flag` + `enabled.PowerDisplay=false` + event signal. (PR #47734)

## Hotplug / wake
- [ ] Wake/hotplug handling locks the UI before rescan and unregisters power notifications on dispose. (PR #47876)

## Serialization / AOT
- [ ] New serialized types/properties registered in the relevant source-gen `JsonSerializerContext`.

## UI / ViewModels
- [ ] Slider commits use the shared `SliderCommitScheduler` debounce (no per-feature ad-hoc timers). (PR #48207)
- [ ] Selection/seed logic is simple (e.g. lowest Windows DISPLAY number, Id fallback); no over-engineered planners. (PR #48207)
- [ ] Live-update: settings changes propagate to open flyout via the settings-updated IPC event.
- [ ] UI hangs → lock the UI, not hidden suppression flags. (PR #48207)

## Localization / style
- [ ] New end-user strings in `Strings/en-us/Resources.resw`, surfaced via `x:Uid`.
- [ ] `.\.pipelines\applyXamlStyling.ps1 -Main` run for XAML changes (CI enforces it).

## Tests
- [ ] Logic changes covered by `PowerDisplay.Lib.UnitTests` (blacklist, crash recovery, identity, migrator, linked-brightness planner, VCP value).
