# Awake PR Review Checklist

Apply after reading the diff cold (see the anti-anchoring note in SKILL.md). Only check rows whose
code paths the PR actually touches.

## Execution state / modes (`Awake/Core/Manager.cs`)
- [ ] `ComputeAwakeState` still ORs in `ES_CONTINUOUS`; display flag stays user-controlled (no forced `ES_DISPLAY_REQUIRED` to "fix" Modern Standby).
- [ ] All state changes go through `_stateQueue` (monitor thread), not direct `Bridge.SetThreadExecutionState` from UI/CLI.
- [ ] New/changed mode setters call `CancelExistingThread()` first.
- [ ] Under `IsUsingPowerToysConfig`, setters persist `Properties.Mode` and **return early** (no inline-apply + save → double execution).
- [ ] Timed mode computes remaining time vs. an absolute `targetExpiryTime` (no per-tick decrement drift).
- [ ] Duration conversions round **up** partial minutes (no early expiry).
- [ ] `SetExpirableKeepAwake` rejects a non-future target; past dates handled (bumped or exited), not armed as-is.
- [ ] `SetDisplay` does not restart a running TIMED timer.

## Power / lifecycle
- [ ] Mode survives resume: covered by `ReapplyAwakeState`; `WM_POWERBROADCAST` handler intact.
- [ ] `CompleteExit` teardown order preserved (revert passive → StopMonitor → dispose timer → remove tray icon → destroy window).

## CLI (`Awake/Program.cs`)
- [ ] `--pid` validator rejects non-integer, non-positive, and non-existent PIDs at parse time; can't bind to self.
- [ ] `--expire-at` parses a date; `--time-limit` bounded.
- [ ] Help/error printed and early-returned **before** tray/monitor setup; console attach/free path correct.
- [ ] `await InvokeAsync` used (no `.Result` blocking).
- [ ] Argument precedence unchanged: `--use-pt-config` > `--pid`/`--use-parent-pid` > `--expire-at` > `--time-limit` > indefinite.

## Tray (`Awake/Core/TrayHelper.cs`)
- [ ] `ShowContextMenu` foregrounds owner window and uses correct `TPM_*` alignment + `MNS_AUTO_DISMISS`.
- [ ] Tray icon re-added on `TaskbarCreated`; `SetModeShellIcon` called after mode changes.
- [ ] Tray/menu state touched only via `RunOnMainThread`.

## P/Invoke (`Awake/Core/Native/Bridge.cs`)
- [ ] Signatures match real Win32 return types (`BOOL` vs `void`), `SetLastError=true` where needed.

## Cross-cutting
- [ ] `AwakeMode`/`AwakeProperties` serialization round-trips; Settings UI (`AwakeViewModel`, `AwakeProperties.cs`) stays in lockstep.
- [ ] New persisted setting has a serialization/CLI test.
- [ ] Telemetry event emitted for new modes/commands where the existing ones do.
