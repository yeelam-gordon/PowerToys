---
name: awake-knowledge
description: 'PowerToys Awake module knowledge: feature->file/function map, recurring regression playbooks (keep-awake lost after sleep/resume, timed mode expiring early / countdown drift, past expiration date, indefinite mode ineffective on Modern Standby S0, tray icon/time-format mismatch, tray context-menu positioning, CLI console output not visible, --pid validation), maintainer review rules, and Pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/awake — SetThreadExecutionState keep-awake, ES_SYSTEM_REQUIRED/ES_DISPLAY_REQUIRED/ES_CONTINUOUS, indefinite/timed/expirable/passive modes, keep display on, system tray, settings JSON, CLI options. Keywords: Awake, keep awake, SetThreadExecutionState, ES_CONTINUOUS, display on, sleep, standby, tray icon, TrackPopupMenu, timed mode, expirable, --pid, --use-pt-config, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Awake Knowledge

Grounded engineering knowledge for the PowerToys **Awake** module — keeps Windows (and optionally
the display) awake by driving the Win32 `SetThreadExecutionState` API. It runs as a standalone
WinExe (`PowerToys.Awake.exe`) with a CLI, a system-tray UI, and a PowerToys-settings-driven mode.
Use it to localize code fast, avoid known regression traps, and enforce the conventions the
maintainers already established.

Source root: [`src/modules/awake/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/awake).
Three projects: `Awake/` (main WinExe + CLI), `Awake.ModuleServices/` (PowerToys service layer),
`AwakeModuleInterface/` (C++ native module bridge).

## When to Use This Skill

- Planning or implementing a change under `src/modules/awake/` and needing prior art.
- Fixing/triaging an Awake bug: system still sleeps, keep-awake lost after resume from sleep,
  timed mode expires early or the countdown drifts, expirable mode set to a past date, indefinite
  mode ineffective on Modern Standby (S0), wrong/stale tray icon, tray menu pops in the wrong place,
  no console output when run from the command line, `--pid`/`--expire-at` not validated.
- Reviewing an Awake PR against maintainer conventions and regression traps.
- Touching the execution-state monitor thread, the mode setters, the Rx timer, the settings
  file-watcher, the tray/menu plumbing, or the CLI option parsing.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| CLI entry, option/alias definitions, help/error early-exit | `Awake/Program.cs` `Main`, `BuildRootCommand` (`_aliasesConfigOption` … `_aliasesParentPidOption`) |
| CLI option validators (`--time-limit`, `--pid`, `--expire-at`) | `Program.cs` `timeOption.AddValidator` / `pidOption.AddValidator` / `expireAtOption.AddValidator`; `ProcessExists` |
| Argument dispatch → mode selection | `Program.cs` `HandleCommandLineArguments` (precedence: `--use-pt-config` > `--pid`/`--use-parent-pid` > `--expire-at` > `--time-limit` > indefinite) |
| Process-scoped keep-awake (bind to PID / parent PID) | `Program.cs` `HandleProcessScopedKeepAwake`; `Manager.GetParentProcess` (`NtQueryInformationProcess`) |
| Settings file-watcher (live config) | `Program.cs` `SetupFileSystemWatcher` (Rx `Throttle(25ms)`), `HandleAwakeConfigChange`, `ProcessSettings` |
| Settings → mode mapping + past-expiry correction | `Program.cs` `ProcessSettings` (`switch settings.Properties.Mode`) |
| **Execution-state core (keep-awake)** | `Awake/Core/Manager.cs` `SetAwakeState` (`Bridge.SetThreadExecutionState`), `ComputeAwakeState` (`ES_SYSTEM_REQUIRED [| ES_DISPLAY_REQUIRED] | ES_CONTINUOUS`) |
| State monitor thread (serializes state changes) | `Manager.cs` `StartMonitor`/`StopMonitor` (`BlockingCollection<ExecutionState> _stateQueue`) |
| Indefinite mode | `Manager.cs` `SetIndefiniteKeepAwake` |
| Timed mode + countdown tray update | `Manager.cs` `SetTimedKeepAwake` (Rx `Observable.Interval(1s)` + `targetExpiryTime`; `TakeWhile(remaining>0)` completes at zero → `Subscribe(onNext, () => HandleTimerCompletion("timed"))` — completion callback MUST be parameterless `()`, not `_ =>`, or it binds to the onError overload and never expires), `HandleTimerCompletion` |
| Expirable (date/time) mode | `Manager.cs` `SetExpirableKeepAwake` (`Observable.Timer(remainingTime)`) |
| Passive mode (off) | `Manager.cs` `SetPassiveKeepAwake` |
| Toggle "keep display on" | `Manager.cs` `SetDisplay` (special-cases TIMED to avoid restarting timer) |
| Re-apply state after power event | `Manager.cs` `ReapplyAwakeState`; called from `TrayHelper.WndProc` `WM_POWERBROADCAST` |
| Clean shutdown / revert to passive | `Manager.cs` `CompleteExit`, `CancelExistingThread` |
| Execution-state flags enum | `Awake/Core/Models/ExecutionState.cs` |
| Tray icon set/update by mode | `Manager.cs` `SetModeShellIcon`; `TrayHelper.SetShellIcon` (`Shell_NotifyIcon`) |
| Tray init, hidden message window, message loop | `Awake/Core/TrayHelper.cs` `InitializeTray`, `RunMessageLoop`, `WndProc` |
| Tray context-menu display/positioning | `TrayHelper.cs` `ShowContextMenu` (`SetForegroundWindow` + `TrackPopupMenuEx`) |
| Tray menu build (modes, times sub-menu, display toggle, exit) | `TrayHelper.cs` `SetTray`, `CreateNewTrayMenu`, `CreateAwakeTimeSubMenu`, `InsertAwakeModeMenuItems` |
| Tray command handling (menu clicks) | `TrayHelper.cs` `WndProc` `WM_COMMAND` (`TrayCommands` enum) |
| Default timed intervals (30m/1h/2h) | `Manager.cs` `GetDefaultTrayOptions` |
| P/Invoke surface | `Awake/Core/Native/Bridge.cs`, `Awake/Core/Native/Constants.cs` |
| Telemetry events | `Awake/Telemetry/*` (`AwakeIndefinitelyKeepAwakeEvent`, `AwakeTimedKeepAwakeEvent`, `AwakeExpirableKeepAwakeEvent`, `AwakeNoKeepAwakeEvent`, `AwakeCLICommandEvent`) |
| Settings UI (C#) — must mirror mode/serialization | `src/settings-ui/.../SettingsXAML/Views/AwakePage.xaml`, `ViewModels/AwakeViewModel.cs`, `Settings.UI.Library/AwakeProperties.cs` |

**Mode is the single source of truth.** Every mode setter under PowerToys config writes
`Properties.Mode` (+ related fields) and **returns early**, letting the file-watcher re-trigger
`ProcessSettings` to actually apply the state — avoiding double execution. Preserve that pattern.

## Regression Playbooks

Rule by rule: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Keep-awake lost after sleep / resume
- **Symptom:** after the machine sleeps and resumes, Awake stops holding the system awake until the
  user disables and re-enables it.
- **Where:** `TrayHelper.WndProc` `WM_POWERBROADCAST`; `Manager.ReapplyAwakeState`; `ComputeAwakeState`.
- **Root cause:** `SetThreadExecutionState` continuous state can be dropped across power transitions;
  without re-arming it, the mode is silently inactive.
- **Guardrail:** on `PBT_APMRESUMEAUTOMATIC`/`PBT_APMRESUMESUSPEND`/`PBT_APMPOWERSTATUSCHANGE`, call
  `ReapplyAwakeState`, which re-queues `ComputeAwakeState(IsDisplayOn)` for any non-passive mode.
  Don't skip the passive-mode guard. Evidence: issue
  [#44642](https://github.com/microsoft/PowerToys/issues/44642); feature
  [PR #44795](https://github.com/microsoft/PowerToys/pull/44795).

### Timed mode never expires (stays awake after countdown hits zero)
- **Symptom:** a timed keep-awake reaches 0 but Awake stays in the TIMED state — the machine keeps
  being kept awake and never reverts to Passive/exits.
- **Where:** `Awake/Core/Manager.cs` `SetTimedKeepAwake`, the terminal `.Subscribe(...)` on the
  `Observable.Interval(1s).Select(...).TakeWhile(remaining => remaining.TotalSeconds > 0)` pipeline.
  Completion should invoke `HandleTimerCompletion("timed")` (which reverts to Passive or exits).
- **Root cause:** the completion callback was written as a discard lambda `_ =>
  HandleTimerCompletion("timed")`, which binds to the `Subscribe(onNext, onError)` overload
  (`Action<Exception>`) instead of `Subscribe(onNext, onCompleted)` (`Action`). When `TakeWhile`
  completed the sequence at zero, the **onCompleted** slot was empty, so completion never fired and
  Awake stayed TIMED. (`Action<Exception>` vs `Action` overload trap — a discard `_` silently
  changed which overload the compiler picked.)
- **Guardrail:** the completion handler must be a **parameterless** lambda `() =>
  HandleTimerCompletion("timed")` so it binds to the `onCompleted` overload. When wiring Rx
  `Subscribe`, be explicit about onNext/onError/onCompleted arity — never let a discard `_` route
  completion logic into the error handler. Evidence: issue
  [#43775](https://github.com/microsoft/PowerToys/issues/43775); fix
  [PR #43785](https://github.com/microsoft/PowerToys/pull/43785).

### Timed mode expires early / doesn't last the full duration
- **Symptom:** a timed keep-awake ends before the requested time (e.g. "30 min" stops at ~29).
- **Where:** `Manager.SetTimedKeepAwake` (settings round-trip); `Program.ProcessSettings`
  (`IntervalHours*3600 + IntervalMinutes*60`).
- **Root cause:** partial minutes were truncated when converting seconds ↔ hours/minutes for the
  settings round-trip, shortening the effective duration.
- **Guardrail:** round **up** partial minutes: `remainingMinutes = (uint)Math.Ceiling(TotalMinutes % 60)`.
  Keep the seconds→settings→seconds conversion loss-free. Evidence:
  [PR #44795](https://github.com/microsoft/PowerToys/pull/44795).

### Countdown timer drift
- **Symptom:** the tray countdown for timed mode slowly diverges from wall-clock over long durations.
- **Where:** `Manager.SetTimedKeepAwake` Rx pipeline.
- **Root cause:** decrementing a counter each tick accumulates scheduler jitter.
- **Guardrail:** compute remaining time against an absolute `targetExpiryTime = Now.AddSeconds(seconds)`
  each tick (`targetExpiryTime - DateTimeOffset.Now`), not by subtracting from a running total.
  `TakeWhile(remaining => remaining.TotalSeconds > 0)` intentionally never ticks a negative value.
  Evidence: [PR #41684](https://github.com/microsoft/PowerToys/pull/41684).

### Expirable mode set to a past date/time
- **Symptom:** an expiration date in the past leaves Awake in a degenerate/immediately-expiring state.
- **Where:** `Manager.SetExpirableKeepAwake` (rejects `expireAt <= Now`); `Program.ProcessSettings`
  EXPIRABLE branch.
- **Root cause:** settings can persist a past `ExpirationDateTime` (e.g. stale config, edited file).
- **Guardrail:** `SetExpirableKeepAwake` returns without arming if the target isn't in the future;
  `ProcessSettings` bumps a past `ExpirationDateTime` to `Now.AddMinutes(5)`, saves, and returns so
  the watcher re-triggers with a valid time. Note the open request to *exit* instead in standalone
  mode. Evidence: issue [#46349](https://github.com/microsoft/PowerToys/issues/46349).

### Indefinite/keep-awake ineffective on Modern Standby (S0) unless "keep display on"
- **Symptom:** on S0 Modern Standby laptops the system still sleeps with indefinite mode on, unless
  the display is also kept on.
- **Where:** `ComputeAwakeState` — `ES_SYSTEM_REQUIRED | ES_CONTINUOUS` vs. adding `ES_DISPLAY_REQUIRED`.
- **Root cause:** platform limitation — on Modern Standby, `ES_SYSTEM_REQUIRED` alone may not keep
  the machine out of S0; the display-required flag changes behavior. Not a code defect Awake can
  fully fix. See
  [SetThreadExecutionState](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate)
  and [Modern Standby](https://learn.microsoft.com/windows-hardware/design/device-experiences/modern-standby).
- **Guardrail:** don't "fix" by forcing `ES_DISPLAY_REQUIRED`; document the limitation and keep the
  display flag user-controlled. Evidence: issues
  [#44458](https://github.com/microsoft/PowerToys/issues/44458),
  [#44286](https://github.com/microsoft/PowerToys/issues/44286).

### Tray context menu appears in the wrong position
- **Symptom:** the tray icon's right/left-click menu pops away from the cursor / taskbar or won't
  dismiss on click-away.
- **Where:** `TrayHelper.ShowContextMenu`.
- **Root cause:** menu shown without bringing the owner window to the foreground and without correct alignment
  flags, so Windows mis-positions it and skips auto-dismiss.
- **Guardrail:** `SetForegroundWindow(hWnd)` before `TrackPopupMenuEx`; use cursor position with
  `TPM_LEFT_ALIGN | TPM_BOTTOMALIGN | TPM_LEFT_BUTTON` and `MNS_AUTO_DISMISS`. Evidence:
  [PR #41009](https://github.com/microsoft/PowerToys/pull/41009).

### No console output / help/errors invisible when run from CLI
- **Symptom:** running `PowerToys.Awake.exe --help` (or with a bad arg) as a WinExe shows nothing.
- **Where:** `Program.Main` (`Bridge.AttachConsole(ATTACH_PARENT_PROCESS)`, early help/error exit);
  `Program.AllocateLocalConsole`; `Manager.AllocateConsole`; `Bridge.FreeConsole`.
- **Root cause:** a WinExe has no console; attaching to the parent console is required, and the later
  `AllocConsole` path stops working after attach unless `FreeConsole` is called first.
- **Guardrail:** attach to the parent console, print help/errors and early-return **before** tray/
  monitor setup; call `FreeConsole` before `AllocateLocalConsole` when no PID is bound. Evidence:
  [PR #41774](https://github.com/microsoft/PowerToys/pull/41774) (issue: CLI help/error text not visible).

## Review Rules

Enforce these when reviewing or authoring Awake changes:

- **Keep the mode setters' "write settings then return early" contract.** Under
  `IsUsingPowerToysConfig`, `SetIndefinite/Timed/Expirable/PassiveKeepAwake` must persist
  `Properties.Mode` and **return**, letting the file-watcher re-run `ProcessSettings`. Applying state
  inline *and* saving causes double execution. Evidence: `Manager.cs` mode setters + `ProcessSettings`.
- **All execution-state changes go through the `_stateQueue` monitor thread.** Don't call
  `Bridge.SetThreadExecutionState` directly from UI/CLI paths; `Add(...)` to `_stateQueue` so
  `StartMonitor` serializes them and can revert to PASSIVE on failure. Evidence: `Manager.StartMonitor`.
- **Re-apply state on power events.** Any new mode must survive resume — verify `ReapplyAwakeState`
  covers it and the `WM_POWERBROADCAST` handler stays wired. Evidence: #44642 / PR #44795.
- **Validate CLI options at parse time, not during execution.** `--pid` must reject non-integers,
  non-positive values, and nonexistent processes (immediate feedback); `--expire-at` must parse a
  date; `--time-limit` bounds. Don't simplify validators to a bare `int.TryParse`. Evidence:
  [PR #41774 review](https://github.com/microsoft/PowerToys/pull/41774).
- **Prefer `await InvokeAsync` over `.Result` on System.CommandLine.** Blocking on `.Result` inside an
  async method risks deadlocks. Evidence: [PR #41774 review](https://github.com/microsoft/PowerToys/pull/41774).
- **Match P/Invoke signatures to the real Win32 return type.** e.g. `FreeConsole` returns `BOOL`, not
  `void`; declare `[return: MarshalAs(UnmanagedType.Bool)] bool` with `SetLastError=true` like
  `AttachConsole`/`AllocConsole`. Evidence: [PR #41774 review](https://github.com/microsoft/PowerToys/pull/41774).
- **Round up (never truncate) when converting durations for the settings round-trip.** Truncated
  partial minutes shorten timed mode. Evidence: [PR #44795](https://github.com/microsoft/PowerToys/pull/44795).
- **Wire Rx `Subscribe` completion to the right overload.** `Subscribe(onNext, () => ...)` is
  onCompleted; `Subscribe(onNext, _ => ...)` silently binds to `onError (Action<Exception>)`. A
  discard `_` on the completion callback leaves onCompleted empty — timed mode then never expires.
  Evidence: [PR #43785](https://github.com/microsoft/PowerToys/pull/43785).
- **Compute countdowns against an absolute target time.** Avoid per-tick decrement accumulation.
  Evidence: [PR #41684](https://github.com/microsoft/PowerToys/pull/41684).
- **`SetDisplay` must not restart a running TIMED timer.** Toggling "keep display on" during timed
  mode updates `IsDisplayOn` + re-queues the execution state but preserves the existing Rx
  subscription and expiry. Evidence: `Manager.SetDisplay`.
- **Re-add the tray icon on `TaskbarCreated`.** Explorer restarts destroy the notification icon;
  `WndProc` must handle the registered `TaskbarCreated` message and `SetModeShellIcon(forceAdd:true)`.
  Evidence: `TrayHelper.WndProc`.
- **Keep C# mode/serialization in lockstep with the Settings UI.** `AwakeMode` and `AwakeProperties`
  serialization drive both `ProcessSettings` and `AwakeViewModel`; a schema change must round-trip.

## Pitfalls

- **Modern Standby (S0) is the #1 "still sleeps" trap.** `ES_SYSTEM_REQUIRED | ES_CONTINUOUS` may not
  hold an S0 laptop awake unless the display is kept on — a Windows platform limitation, not an Awake
  bug. Don't silently force `ES_DISPLAY_REQUIRED`; keep it user-controlled (#44458, #44286).
- **`ES_CONTINUOUS` is mandatory.** Without it, `SetThreadExecutionState` sets a one-shot reset
  instead of a persistent state; `ComputeAwakeState` always ORs it in.
- **State is dropped across sleep/resume.** Awake re-arms via `WM_POWERBROADCAST` →
  `ReapplyAwakeState`; a new mode that bypasses that path will die on resume (#44642).
- **The monitor thread is `IsBackground = false`** and blocks on `_stateQueue.Take`; forgetting to
  `StopMonitor()` / drain it can keep the process alive. `CompleteExit` handles teardown order
  (revert passive → stop monitor → dispose timer → remove tray icon → destroy window).
- **`CancelExistingThread` doesn't kill a thread** — it enqueues `ES_CONTINUOUS` (reset) and disposes
  the Rx `_timerSubscription`. Every mode setter calls it first to clear prior timers.
- **Awake cannot bind to its own PID** — `HandleProcessScopedKeepAwake` rejects `pid == Environment.ProcessId`
  (would be an indefinite keep-awake). `--pid` and `--use-parent-pid`: explicit `--pid` wins.
- **`--use-pt-config` overrides all other CLI args** — when set, args other than `--pid` are ignored
  and behavior is driven entirely by the settings file + watcher.
- **The tray uses a hidden message-only-style window on a dedicated STA thread** with a single-thread
  sync context; touch tray/menu state only via `RunOnMainThread`.
- **Tray countdown truncates to whole seconds** (`(uint)remainingTimeSpan.TotalSeconds`); the
  `TakeWhile(> 0)` means "00s" is typically never rendered before completion — expected, not a bug.
- **Tray icon can show stale/wrong mode** if `SetModeShellIcon` isn't called after a mode change, or
  after Explorer restart without the `TaskbarCreated` re-add (#46079).
- **Tray hover time text uses the current culture's time format** — 24h/AM-PM and localization gaps
  surface here (#47359, #48259).

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to an Awake PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/awake/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/awake) · [Awake docs](https://learn.microsoft.com/windows/powertoys/awake)
- [SetThreadExecutionState](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate) · [Modern Standby](https://learn.microsoft.com/windows-hardware/design/device-experiences/modern-standby) · [TrackPopupMenuEx](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-trackpopupmenuex) · [WM_POWERBROADCAST](https://learn.microsoft.com/windows/win32/power/wm-powerbroadcast)
