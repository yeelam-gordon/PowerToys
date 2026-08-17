# CmdNotFound Evidence & Decision Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split:** `SKILL.md` owns the actionable symptom → cause → guardrail playbooks. This catalog
> retains provenance, source coordinates, chronology, migration/reviewer decisions, unresolved
> clusters, and evidence caveats without repeating those explanations.

## Product and migration chronology

Ordered by the referenced PR sequence; no merge dates are asserted here.

| Artifact | Exact source locations | Recorded outcome / decision |
|---|---|---|
| [PR #26319](https://github.com/microsoft/PowerToys/pull/26319) | `CmdNotFoundModuleInterface/dllmain.cpp`; historical bundled PowerShell module | Introduced Command Not Found. |
| [PR #30727](https://github.com/microsoft/PowerToys/pull/30727) | Settings install flow and bundled scripts | Installation-workflow improvement; inspect the PR diff for the exact revision because the prior catalog did not preserve more detail. |
| [PR #30745](https://github.com/microsoft/PowerToys/pull/30745) | Historical `src/modules/cmdNotFound/CmdNotFound/WinGetCommandNotFoundFeedbackPredictor.cs`, constructor and `GetFeedback(FeedbackContext, CancellationToken)` | Added `Logger.InitializeLogger("\\CmdNotFound\\Logs")`, exception logging, and a graceful `FeedbackItem`; the source file later left this repo in #32766. |
| [PR #30759](https://github.com/microsoft/PowerToys/pull/30759) | PowerShell installation/platform gating | Disabled ARM64 while a PowerShell 7.4 MSI was unavailable. |
| [PR #32034](https://github.com/microsoft/PowerToys/pull/32034) | `CmdNotFoundViewModel.RunPowerShellOrPreviewScript`; `pwsh-preview.cmd` discovery | Added PowerShell Preview support. |
| [PR #32766](https://github.com/microsoft/PowerToys/pull/32766) | `Assets/Settings/Scripts/EnableModule.ps1`, `DisableModule.ps1`, `CheckCmdNotFoundRequirements.ps1`; removed historical predictor project | Migrated registration to the PowerShell Gallery package `Microsoft.WinGet.CommandNotFound`, retained an in-place legacy-profile upgrade, and re-enabled ARM64. Discussion distinguished `Update-Module` for an installed module from `Install-Module` when absent. |
| [PR #32892](https://github.com/microsoft/PowerToys/pull/32892) | CmdNotFound Settings page initialization | Fixed page initialization on ARM. |
| [PR #37690](https://github.com/microsoft/PowerToys/pull/37690) | `Assets/Settings/Scripts/EnableModule.ps1` | Gated `Enable-ExperimentalFeature` calls on feature discovery. |
| [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) | Module project files | Carried the repo-wide `$(RepoRoot)` path convention into this module. |

## Contract and decision ledger

| Contract / decision | Exact source locations | Evidence |
|---|---|---|
| Native enable/disable shells out to PowerShell scripts; runtime suggestions are not served by a daemon. | `CmdNotFoundModuleInterface/dllmain.cpp` constructor, `install_module()`, `uninstall_module()`; `EnableModule.ps1`, `DisableModule.ps1` | [PR #26319](https://github.com/microsoft/PowerToys/pull/26319), current-source inspection recorded by the original catalog |
| Current and legacy profile markers are stable migration identifiers: `f45873b3-b655-43a6-b217-97c00aa0db58` and `34de4b3d-13a8-4540-b76d-b9e8d3851756`. | `EnableModule.ps1`, `DisableModule.ps1`, `CheckCmdNotFoundRequirements.ps1` | [PR #32766](https://github.com/microsoft/PowerToys/pull/32766) |
| Requirement state is derived from invariant stdout strings consumed by C# `Contains(...)` checks. | Compared `Write-Host` lines in `CheckCmdNotFoundRequirements.ps1` / install scripts; `CmdNotFoundViewModel.CheckCommandNotFoundRequirements()` | Current-source inspection recorded by the original catalog |
| WinGet.Client floor is `1.8.1133`. | `CheckCmdNotFoundRequirements.ps1`, `InstallWinGetClientModule.ps1`, `EnableModule.ps1` | Current-source inspection recorded by the original catalog |
| Persisted settings are minimal (`Version = "1"`, name `CmdNotFound`); operational state comes from GPO/profile presence. | `Settings.UI.Library/CmdNotFoundSettings.cs`; native and managed GPO checks | Current-source inspection recorded by the original catalog |

## Issue and unresolved-cluster ledger

| Cluster | Evidence | Exact source locations | Ledger status |
|---|---|---|---|
| Offline/DNS/startup failures | [#33065](https://github.com/microsoft/PowerToys/issues/33065), [#33061](https://github.com/microsoft/PowerToys/issues/33061), [#34286](https://github.com/microsoft/PowerToys/issues/34286), [#33251](https://github.com/microsoft/PowerToys/issues/33251), [#33304](https://github.com/microsoft/PowerToys/issues/33304), [#33669](https://github.com/microsoft/PowerToys/issues/33669), [#39302](https://github.com/microsoft/PowerToys/issues/39302); mitigations [#30745](https://github.com/microsoft/PowerToys/pull/30745), [#32766](https://github.com/microsoft/PowerToys/pull/32766) | Historical predictor `GetFeedback`; current external `Microsoft.WinGet.CommandNotFound`; profile registration in `EnableModule.ps1` | Repeated issue cluster spans both the removed in-repo predictor and external Gallery era. This repository alone cannot prove current external-module resolution. |
| Predictor runtime diagnostics / Store-MSIX limitation | [PR #30745](https://github.com/microsoft/PowerToys/pull/30745) | Historical `WinGetCommandNotFoundFeedbackPredictor` constructor and `GetFeedback` | Fix verified against the PR diff in the original collection; file removed from current source by #32766. |
| Unwanted `$PROFILE` creation | [#32508](https://github.com/microsoft/PowerToys/issues/32508), [#42365](https://github.com/microsoft/PowerToys/issues/42365) | `CheckCmdNotFoundRequirements.ps1` and `EnableModule.ps1`, `Test-Path $PROFILE` / `New-Item` branches | The requirements-check path was identified as the opt-in boundary concern. Confirm current script contents and issue state before asserting resolution. |
| Synchronous Settings/OOBE work | [#38197](https://github.com/microsoft/PowerToys/issues/38197), [#33179](https://github.com/microsoft/PowerToys/issues/33179), [#33178](https://github.com/microsoft/PowerToys/issues/33178) | `CmdNotFoundViewModel.InitializeEnabledValue`, `CheckCommandNotFoundRequirements`, `RunPowerShellScript` | Issue cluster; the prior catalog did not identify a closing fix PR. |
| Encoding/localization output | [#37663](https://github.com/microsoft/PowerToys/issues/37663), [#34856](https://github.com/microsoft/PowerToys/issues/34856) | `CmdNotFoundViewModel.RunPowerShellScript` (`NO_COLOR=1`, stdout capture); compared script status lines | Issue cluster; invariant string matching remains a source contract. |
| ARM64, Preview, and Store packaging | [#30759](https://github.com/microsoft/PowerToys/pull/30759), [#32034](https://github.com/microsoft/PowerToys/pull/32034), [#32766](https://github.com/microsoft/PowerToys/pull/32766), [#32892](https://github.com/microsoft/PowerToys/pull/32892), [#31935](https://github.com/microsoft/PowerToys/issues/31935), [#36494](https://github.com/microsoft/PowerToys/issues/36494) | `InstallPowerShell7.ps1`; `RunPowerShellOrPreviewScript`; Settings page initialization; historical predictor/WinGet COM boundary | ARM64 and Preview have explicit PR history; Store/packaged-install reports remain separate evidence and may involve the external module. |
| WinGet.Client acquisition/trust | [#31914](https://github.com/microsoft/PowerToys/issues/31914), [#31378](https://github.com/microsoft/PowerToys/issues/31378) | `InstallWinGetClientModule.ps1`; requirement and enable scripts | Issue evidence only in this catalog; no closing fix PR is recorded. |

## Reviewer and migration decisions

- [PR #32766](https://github.com/microsoft/PowerToys/pull/32766) records that the suggestion module is
  built/signed outside PowerToys and published through the PowerShell Gallery; PowerToys owns
  installation, profile registration, detection, upgrade, and removal.
- The same PR preserves legacy-marker detection so existing profiles can be upgraded in place.
- Its discussion distinguishes module update from first installation: `Update-Module` when present,
  `Install-Module` when absent.
- [PR #30745](https://github.com/microsoft/PowerToys/pull/30745) records the user-facing fallback for
  predictor exceptions and identifies PowerShell 7 Store/MSIX WinGet availability as a known case.

## Evidence-quality notes

- “Fix PRs verified via `git log` on module files” is retained from the original catalog's
  collection method; this refactor did not independently replay that history.
- Historical predictor evidence applies to code removed by
  [PR #32766](https://github.com/microsoft/PowerToys/pull/32766). Current suggestion behavior belongs
  to the external `Microsoft.WinGet.CommandNotFound` package and must not be inferred solely from old
  PowerToys source.
- Issue reports establish observed environments and symptoms, not a single cause or current
  reproducibility. Where no fix PR is listed, this ledger deliberately leaves resolution open.
- Excluded as module-noise: CppWinRT sweeps
  [#45420](https://github.com/microsoft/PowerToys/pull/45420) and
  [#31396](https://github.com/microsoft/PowerToys/pull/31396), PCH unification
  [#31055](https://github.com/microsoft/PowerToys/pull/31055), and VS 2026 support
  [#44304](https://github.com/microsoft/PowerToys/pull/44304). Their review comments concern general
  C++/WinRT/build behavior; only the durable `$(RepoRoot)` decision is retained above.
- CI commands, approvals, praise, merge coordination, and formatting-only comments were not treated
  as behavioral evidence.
