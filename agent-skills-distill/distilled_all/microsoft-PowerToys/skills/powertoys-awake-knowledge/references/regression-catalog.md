# Awake Regression & Decision Catalog

Fuller list backing the Regression Playbooks in SKILL.md. Grounded in the module's PRs, issues, and
current source under `src/modules/awake/`. Progressive disclosure — load when you need more than the
top playbooks.

## Regressions & fixes

### R1 — Keep-awake lost after resume from sleep
- **Issue:** [#44642](https://github.com/microsoft/PowerToys/issues/44642) (dup cluster; also #44814).
- **Fix:** [PR #44795](https://github.com/microsoft/PowerToys/pull/44795) "Awake and DevEx improvements".
- **Mechanism:** `TrayHelper.WndProc` handles `WM_POWERBROADCAST` (`PBT_APMRESUMEAUTOMATIC`,
  `PBT_APMRESUMESUSPEND`, `PBT_APMPOWERSTATUSCHANGE`) → `Manager.ReapplyAwakeState`, which re-queues
  `ComputeAwakeState(IsDisplayOn)` for any non-passive mode.
- **Guardrail:** any new mode must survive resume through this path.

### R2 — Timed mode not expiring (stuck in TIMED after countdown hits zero)
- **Fix:** [PR #43785](https://github.com/microsoft/PowerToys/pull/43785) "[Awake] Fix issue with timed mode not expiring correctly" (closes [#43775](https://github.com/microsoft/PowerToys/issues/43775)).
- **Symptom:** the timed countdown reaches 0 but Awake remains in the TIMED state and never reverts
  to Passive / exits.
- **Mechanism:** in `SetTimedKeepAwake`, the pipeline
  `Observable.Interval(1s).Select(...).TakeWhile(remaining => remaining.TotalSeconds > 0)` completes
  when time runs out, and `.Subscribe(onNext, () => HandleTimerCompletion("timed"))` is meant to run
  the completion handler. The bug used a **discard** lambda `_ => HandleTimerCompletion("timed")`,
  which binds to the `Subscribe(onNext, onError)` overload (`Action<Exception>`) rather than the
  `onCompleted` overload (`Action`). So when the sequence completed, nothing was wired to onCompleted
  and the completion logic never ran. The one-line fix changes `_ =>` to `() =>`.
- **Guardrail:** when calling Rx `Subscribe`, be explicit about the onNext/onError/onCompleted arity;
  a discard `_` on a completion callback silently reroutes it to the error handler. Verified against
  the PR diff (single-line change in `Awake/Core/Manager.cs`).

### R2b — Timed mode expiring *early* (duration truncated)
- **Fix:** [PR #44795](https://github.com/microsoft/PowerToys/pull/44795) "Awake and DevEx improvements".
- **Mechanism:** in `SetTimedKeepAwake`, seconds are converted to hours/minutes for the settings
  round-trip; `remainingMinutes = (uint)Math.Ceiling(TotalMinutes % 60)` prevents truncation that
  shortened the duration. `ProcessSettings` recombines `IntervalHours*3600 + IntervalMinutes*60`.

### R3 — Countdown timer drift
- **Fix:** [PR #41684](https://github.com/microsoft/PowerToys/pull/41684) "[Awake] Fix for countdown timer drift".
- **Mechanism:** `Observable.Interval(1s).Select(_ => targetExpiryTime - Now)` measures against an
  absolute expiry rather than decrementing a counter. Review discussion confirmed `TakeWhile(> 0)`
  guarantees the tick body never runs with a negative remaining value, so `(uint)TotalSeconds` is
  safe; "00s" is generally not rendered before completion (accepted behavior).

### R4 — Expirable mode with a past expiration
- **Issue:** [#46349](https://github.com/microsoft/PowerToys/issues/46349) "Awake should exit if a past expiry time is provided" (open — requests *exit* in standalone mode).
- **Current behavior:** `SetExpirableKeepAwake` returns without arming if `expireAt <= Now`;
  `ProcessSettings` bumps a past `ExpirationDateTime` to `Now.AddMinutes(5)`, saves, and returns so
  the watcher re-triggers. Gap: standalone CLI exit-on-past-date not yet implemented.

### R5 — Modern Standby (S0) ineffective without "keep display on"
- **Issues:** [#44458](https://github.com/microsoft/PowerToys/issues/44458),
  [#44286](https://github.com/microsoft/PowerToys/issues/44286) (closed: platform / Feedback Hub).
- **Nature:** Windows platform limitation — `ES_SYSTEM_REQUIRED` alone may not keep an S0 machine
  awake; `ES_DISPLAY_REQUIRED` changes behavior. Not fully fixable in Awake. Keep the display flag
  user-controlled; document the limitation.

### R6 — Tray context-menu positioning
- **Fix:** [PR #41009](https://github.com/microsoft/PowerToys/pull/41009) "Fix Awake's popup context menu positioning".
- **Mechanism:** `ShowContextMenu` calls `SetForegroundWindow(hWnd)` then `TrackPopupMenuEx` with
  `TPM_LEFT_ALIGN | TPM_BOTTOMALIGN | TPM_LEFT_BUTTON` at cursor position, and sets
  `MNS_AUTO_DISMISS` via `SetMenuInfo`.

### R7 — CLI help/error text not visible; console behavior
- **Fix:** [PR #41774](https://github.com/microsoft/PowerToys/pull/41774) "[Awake] Fix issues with help and error text not being visible when running Awake via the command line".
- **Mechanism:** `Main` calls `Bridge.AttachConsole(ATTACH_PARENT_PROCESS)`, prints help/errors and
  early-returns before tray/monitor setup. `FreeConsole` is called before `AllocateLocalConsole`
  because `AllocConsole` stopped working after the parent-attach change (author note in PR thread).
  Known cosmetic: after exit the parent console cursor may need an extra Enter (accepted).

### R8 — Wrong/stale tray icon
- **Issue:** [#46079](https://github.com/microsoft/PowerToys/issues/46079) (closed).
- **Mechanism:** `SetModeShellIcon` chooses the per-mode icon and text; `WndProc` re-adds the icon on
  the registered `TaskbarCreated` message (`SetModeShellIcon(forceAdd:true)`) after Explorer restarts.

## Review-comment-derived conventions

From `review_comments.json` on Awake PRs (maintainers/Copilot, high-signal only):

- **Validate CLI options at parse time** (`--pid` positive + existing process; `--expire-at` parseable) —
  don't defer to execution. `daverayment` agreed to restore the fuller `--pid` validation. (PR #41774)
- **Use `await InvokeAsync`, not `.Result`**, on `System.CommandLine` to avoid deadlocks. `daverayment`
  agreed it was the right call despite legacy behavior. (PR #41774)
- **P/Invoke return types must match Win32** — `FreeConsole` returns `BOOL`; declare it like
  `AttachConsole`/`AllocConsole` with `SetLastError=true` and `MarshalAs(UnmanagedType.Bool)`. (PR #41774)
- **Deliberate early-exit for help/parse-errors** is intentional and placed before Awake setup —
  don't "dedupe" it away as redundant with the later command invocation. (PR #41774, author rationale)

## Key design decisions

- **Mode is the source of truth; setters return early under PT config.** Every mode setter, when
  `IsUsingPowerToysConfig`, writes `Properties.Mode` (+ fields) and returns, letting the
  `FileSystemWatcher` → `ProcessSettings` apply state once. Prevents double execution.
- **Single serialized execution-state channel.** `StartMonitor` consumes a
  `BlockingCollection<ExecutionState>` (`_stateQueue`) on a dedicated foreground thread and reverts to
  PASSIVE on `SetThreadExecutionState` failure.
- **Rx for timers.** Timed mode uses `Observable.Interval`; expirable uses `Observable.Timer`;
  `CancelExistingThread` disposes `_timerSubscription` before starting a new mode.
- **Config-file live reload is throttled** (`Throttle(25ms)`) to coalesce rapid file-write events.
- **CLI arg precedence:** `--use-pt-config` (overrides all but `--pid`) > `--pid`/`--use-parent-pid`
  > `--expire-at` > `--time-limit` > indefinite default.

## Excluded as noise (not distilled)

- Build/infra PRs unrelated to Awake behavior: .NET 10 upgrade (#41280), VS 2026 support (#44304),
  CppWinRT deps bump (#45420), `$(RepoRoot)` path refactor (#44639), global `SettingsUtils` instance
  (#44064), CLI telemetry plumbing (#46872), CmdPal extension (#44006).
- Auto-generated crash-dump / "Bug report (Auto)" issues with no reproduction
  (#45068, #44644, #44190, #44116, #46089, #45229, #44565, #44248, #44515).
- Non-Awake or duplicate/other-module reports (PowerToys Run: #45664, #45283; #44615).
- CI chatter (`/azp run`, azure-pipelines bot), "LGTM", and formatting nits.
