# Awake Evidence & Decision Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split:** `SKILL.md` owns the actionable symptom → cause → guardrail playbooks. This catalog
> retains provenance, source coordinates, chronology, reviewer decisions, unresolved clusters,
> and evidence caveats without repeating those explanations.

## Fix and decision chronology

Ordered by the referenced PR sequence; no merge dates are asserted here.

| Artifact | Exact source locations | Recorded outcome / decision |
|---|---|---|
| [PR #41009](https://github.com/microsoft/PowerToys/pull/41009) | `Awake/Core/TrayHelper.cs` `ShowContextMenu` | Adopted `SetForegroundWindow`, cursor coordinates, `TPM_LEFT_ALIGN \| TPM_BOTTOMALIGN \| TPM_LEFT_BUTTON`, and `MNS_AUTO_DISMISS` for the tray menu. |
| [PR #41684](https://github.com/microsoft/PowerToys/pull/41684) | `Awake/Core/Manager.cs` `SetTimedKeepAwake` | Changed countdown calculation to an absolute expiry. Review accepted that `TakeWhile(remaining > 0)` normally completes without rendering `00s` and prevents a negative value from reaching the unsigned-seconds conversion. |
| [PR #41774](https://github.com/microsoft/PowerToys/pull/41774) | `Awake/Program.cs` `Main`, CLI validators and command invocation; `Awake/Core/Native/Bridge.cs`; local-console allocation path | Restored visible CLI help/errors through parent-console attachment and an intentional pre-setup early exit. Author/reviewer decisions retained full `--pid` validation, preferred `await InvokeAsync` over `.Result`, matched `FreeConsole` to Win32 `BOOL`, and required `FreeConsole` before later `AllocConsole`. |
| [issue #43775](https://github.com/microsoft/PowerToys/issues/43775), [PR #43785](https://github.com/microsoft/PowerToys/pull/43785) | `Awake/Core/Manager.cs` `SetTimedKeepAwake`, terminal Rx `Subscribe` call | One-line callback-arity correction from `_ =>` to `() =>`; verified in the original collection against the PR diff. |
| [issue #44642](https://github.com/microsoft/PowerToys/issues/44642), duplicate cluster [#44814](https://github.com/microsoft/PowerToys/issues/44814), [PR #44795](https://github.com/microsoft/PowerToys/pull/44795) | `Awake/Core/TrayHelper.cs` `WndProc` power broadcasts; `Awake/Core/Manager.cs` `ReapplyAwakeState`, `SetTimedKeepAwake`; `Awake/Program.cs` `ProcessSettings` | Added resume/power-status reapplication and rounded partial timed minutes upward during the settings round-trip. |
| [issue #46079](https://github.com/microsoft/PowerToys/issues/46079) (closed) | `Manager.cs` `SetModeShellIcon`; `TrayHelper.cs` `WndProc` registered `TaskbarCreated` message | Current source selects mode-specific icon/text and force-adds the icon after Explorer recreation. No fix PR is identified in this catalog. |

## Open and limitation ledger

| Cluster | Evidence | Exact source locations | Status recorded by the evidence |
|---|---|---|---|
| Past expiration in standalone mode | [#46349](https://github.com/microsoft/PowerToys/issues/46349) (open in the source catalog) | `Manager.cs` `SetExpirableKeepAwake`; `Program.cs` `ProcessSettings` EXPIRABLE branch | The request to exit for a past date in standalone mode was unresolved; current settings-driven behavior instead corrects a persisted past value and lets the watcher retry. |
| Modern Standby S0 | [#44458](https://github.com/microsoft/PowerToys/issues/44458), [#44286](https://github.com/microsoft/PowerToys/issues/44286) (closed as platform/Feedback Hub) | `Manager.cs` `ComputeAwakeState`; `ExecutionState` flags | Platform-limitation evidence. The catalog does not establish an Awake-only fix for systems where `ES_SYSTEM_REQUIRED` is insufficient without `ES_DISPLAY_REQUIRED`. |
| CLI cursor cosmetic after parent-console detach | [PR #41774](https://github.com/microsoft/PowerToys/pull/41774) discussion | `Program.cs` `Main`; `Bridge.cs` console P/Invokes | Author thread accepted that the parent console may require an extra Enter after exit. |

## Reviewer-decision ledger

| Review decision | Evidence | Coordinates |
|---|---|---|
| Preserve parse-time validation for positive, existing `--pid` values and parsable `--expire-at` values. | [PR #41774](https://github.com/microsoft/PowerToys/pull/41774); author agreed to restore fuller PID validation. | `Program.cs` option validators, `ProcessExists` |
| Keep the help/parse-error early exit before tray and monitor initialization; it is not redundant with later invocation. | [PR #41774](https://github.com/microsoft/PowerToys/pull/41774) author rationale. | `Program.cs` `Main` |
| Use asynchronous command invocation rather than blocking `.Result`. | [PR #41774](https://github.com/microsoft/PowerToys/pull/41774) review. | `Program.cs` `Main` / root-command invocation |
| Match P/Invoke signatures to Win32, including `FreeConsole` returning marshalled `BOOL` with `SetLastError=true`. | [PR #41774](https://github.com/microsoft/PowerToys/pull/41774) review. | `Awake/Core/Native/Bridge.cs` |
| Treat Rx callback arity as overload-significant. | [PR #43785](https://github.com/microsoft/PowerToys/pull/43785) diff. | `Manager.cs` `SetTimedKeepAwake` |

## Durable implementation decisions

These entries identify where the decisions live; operational guidance remains in `SKILL.md`.

| Decision | Exact source locations |
|---|---|
| PowerToys-config mode setters persist mode/fields and return; the file watcher performs application. | `Manager.cs` mode setters; `Program.cs` `SetupFileSystemWatcher`, `ProcessSettings` |
| Execution-state writes are serialized through a dedicated foreground monitor thread and `_stateQueue`. | `Manager.cs` `StartMonitor`, `StopMonitor`, `SetAwakeState` |
| Timed and expirable lifetimes use Rx subscriptions disposed by mode transitions. | `Manager.cs` `SetTimedKeepAwake`, `SetExpirableKeepAwake`, `CancelExistingThread` |
| Settings reload events are coalesced. | `Program.cs` `SetupFileSystemWatcher` (`Throttle(25ms)`) |
| CLI precedence is config, process binding, expiration, time limit, then indefinite. | `Program.cs` `HandleCommandLineArguments` |

## Evidence-quality notes

- The timed-mode callback claim was checked against the single-line PR diff; other entries combine
  issue/PR text with source inspection and should be reconfirmed against the current branch.
- Issue state labels above are retained only where the prior catalog stated them explicitly.
- Issue reports establish symptoms, not universal reproduction or sole causality; the S0 reports
  especially depend on Windows power-model behavior and hardware.
- Excluded as non-behavioral evidence: .NET 10 upgrade [#41280](https://github.com/microsoft/PowerToys/pull/41280),
  VS 2026 support [#44304](https://github.com/microsoft/PowerToys/pull/44304), CppWinRT dependency
  bump [#45420](https://github.com/microsoft/PowerToys/pull/45420), `$(RepoRoot)` refactor
  [#44639](https://github.com/microsoft/PowerToys/pull/44639), global `SettingsUtils`
  [#44064](https://github.com/microsoft/PowerToys/pull/44064), CLI telemetry
  [#46872](https://github.com/microsoft/PowerToys/pull/46872), and CmdPal extension
  [#44006](https://github.com/microsoft/PowerToys/pull/44006).
- Auto-generated crash reports without a reproducible Awake-specific chain were not distilled:
  [#45068](https://github.com/microsoft/PowerToys/issues/45068),
  [#44644](https://github.com/microsoft/PowerToys/issues/44644),
  [#44190](https://github.com/microsoft/PowerToys/issues/44190),
  [#44116](https://github.com/microsoft/PowerToys/issues/44116),
  [#46089](https://github.com/microsoft/PowerToys/issues/46089),
  [#45229](https://github.com/microsoft/PowerToys/issues/45229),
  [#44565](https://github.com/microsoft/PowerToys/issues/44565),
  [#44248](https://github.com/microsoft/PowerToys/issues/44248), and
  [#44515](https://github.com/microsoft/PowerToys/issues/44515).
- PowerToys Run/non-Awake reports [#45664](https://github.com/microsoft/PowerToys/issues/45664),
  [#45283](https://github.com/microsoft/PowerToys/issues/45283), and duplicate/other-module
  [#44615](https://github.com/microsoft/PowerToys/issues/44615) were excluded, as were CI commands,
  approvals, and formatting-only comments.
