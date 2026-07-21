# CmdNotFound Regression & Decision Catalog

Progressive-disclosure companion to SKILL.md. Every entry is grounded in PowerToys source and
issue/PR history. Fix PRs verified via `git log` on the module files.

## Architecture decisions (durable)

- **CmdNotFound is a profile-registered PowerShell module, not a daemon.** The native
  `CmdNotFoundModuleInterface` DLL only shells out to `pwsh.exe` running `EnableModule.ps1` /
  `DisableModule.ps1` on GPO-driven enable/disable; runtime behavior lives entirely in the user's
  `$PROFILE` and the external suggestion module. Introduced [#26319](https://github.com/microsoft/PowerToys/pull/26319).
- **The suggestion module moved out of PowerToys** into its own repo, built/signed and published to
  the PowerShell Gallery as **`Microsoft.WinGet.CommandNotFound`**. PowerToys switched from bundling
  `WinGetCommandNotFound.psd1` to registering the Gallery module, with an in-place `$PROFILE` upgrade
  path. [#32766](https://github.com/microsoft/PowerToys/pull/32766) (upgrade + ARM64 re-enable).
- **Two profile-marker GUIDs form a contract:** current `f45873b3-b655-43a6-b217-97c00aa0db58`,
  legacy `34de4b3d-13a8-4540-b76d-b9e8d3851756`. String-searched in `EnableModule.ps1` /
  `DisableModule.ps1` / `CheckCmdNotFoundRequirements.ps1`. Never regenerate.
- **Requirement detection is stdout-string matching.** `CmdNotFoundViewModel` reads `pwsh.exe`
  stdout and branches on English `Contains(...)` substrings; each such line is annotated in the
  `.ps1` as a compared string. This couples script wording and C# tightly by design.
- **WinGet.Client version floor = `1.8.1133`**, checked identically in
  `CheckCmdNotFoundRequirements.ps1`, `InstallWinGetClientModule.ps1`, and `EnableModule.ps1`.
- **Minimal persisted settings.** `CmdNotFoundSettings` (`Version = "1"`, name `CmdNotFound`) stores
  almost nothing; enable state is driven by GPO + profile presence, not a settings flag.

## Regression classes (with evidence)

### 1. Offline / DNS-failure startup crash (highest frequency)
- Issues: [#33065](https://github.com/microsoft/PowerToys/issues/33065),
  [#33061](https://github.com/microsoft/PowerToys/issues/33061),
  [#34286](https://github.com/microsoft/PowerToys/issues/34286),
  [#33251](https://github.com/microsoft/PowerToys/issues/33251),
  [#33304](https://github.com/microsoft/PowerToys/issues/33304),
  [#33669](https://github.com/microsoft/PowerToys/issues/33669),
  [#39302](https://github.com/microsoft/PowerToys/issues/39302).
- Root cause: suggestion/WinGet lookup exceptions propagate into pwsh startup.
- Mitigations: log + runtime error-handling in the C# predictor
  [#30745](https://github.com/microsoft/PowerToys/pull/30745) (see class 1b below);
  migration to the maintained Gallery module [#32766](https://github.com/microsoft/PowerToys/pull/32766).

### 1b. GetFeedback runtime failure — no logging, exception not caught
- Fix: [#30745](https://github.com/microsoft/PowerToys/pull/30745) "[CmdNotFound] Log and runtime error handling".
- Where: `WinGetCommandNotFoundFeedbackPredictor.cs` — method `GetFeedback(FeedbackContext, CancellationToken)`
  and the `WinGetCommandNotFoundFeedbackPredictor(string guid)` constructor (historical path
  `src/modules/cmdNotFound/CmdNotFound/`; file later removed from the repo by #32766 when the module
  moved to the PSGallery release).
- Mechanism: before the fix, `GetFeedback` executed `FindPackages` and built the `winget install --id …`
  candidate list with **no try/catch and no logger**, so any runtime error (e.g. WinGet COM
  unavailable when PS7 is installed from the Store/MSIX) threw uncaught and silently. The fix:
  (1) constructor calls `Logger.InitializeLogger("\\CmdNotFound\\Logs")`; (2) the `GetFeedback` body is
  wrapped in `try { … } catch (Exception ex) { Logger.LogError("GetFeedback failed to execute", ex);
  return new FeedbackItem("Failed to execute PowerToys Command Not Found. …", …); }` — it logs and
  returns a graceful user-facing `FeedbackItem` (naming the known PS7 Store/MSIX limitation) instead of
  throwing into the prompt. Verified against the PR diff.

### 2. Unwanted `$PROFILE` creation
- Issues: [#32508](https://github.com/microsoft/PowerToys/issues/32508) (prevent empty `$PROFILE`
  unless installed), [#42365](https://github.com/microsoft/PowerToys/issues/42365) (creating
  `Documents\PowerShell` breaks Explorer address-bar `powershell`).
- Where: `Test-Path $PROFILE` + `New-Item` in both `CheckCmdNotFoundRequirements.ps1` and
  `EnableModule.ps1`. The check path is the offender.

### 3. UI-thread / slow install
- Issues: [#38197](https://github.com/microsoft/PowerToys/issues/38197) (installs on UI thread),
  [#33179](https://github.com/microsoft/PowerToys/issues/33179),
  [#33178](https://github.com/microsoft/PowerToys/issues/33178) (install too long).
- Where: `CmdNotFoundViewModel` ctor → synchronous `RunPowerShellScript` stdout loop.

### 4. Encoding / localization of install output
- Issues: [#37663](https://github.com/microsoft/PowerToys/issues/37663) (weird characters),
  [#34856](https://github.com/microsoft/PowerToys/issues/34856) (Chinese env garbled log).
- Where: `RunPowerShellScript` (`NO_COLOR=1`) + English-substring detection contract.

### 5. Experimental-feature enablement
- Fix: [#37690](https://github.com/microsoft/PowerToys/pull/37690) — only enable
  `PSFeedbackProvider` / `PSCommandNotFoundSuggestion` if `Get-ExperimentalFeature` lists them.

### 6. Platform coverage (ARM64 / PS Preview / Store)
- [#30759](https://github.com/microsoft/PowerToys/pull/30759) disabled on ARM64 (no PS 7.4 MSI);
  re-enabled in [#32766](https://github.com/microsoft/PowerToys/pull/32766).
- PS-Preview support [#32034](https://github.com/microsoft/PowerToys/pull/32034); ARM64 page-init
  fix [#32892](https://github.com/microsoft/PowerToys/pull/32892).
- Store/packaged install failures: [#31935](https://github.com/microsoft/PowerToys/issues/31935),
  [#36494](https://github.com/microsoft/PowerToys/issues/36494).

### 7. WinGet.Client acquisition
- [#31914](https://github.com/microsoft/PowerToys/issues/31914) (install error),
  [#31378](https://github.com/microsoft/PowerToys/issues/31378) ("untrusted repository").

## Grounded fix/feature PRs (from `git log` on module files)

| PR | What |
|---|---|
| [#26319](https://github.com/microsoft/PowerToys/pull/26319) | Introduce Command Not Found module |
| [#30727](https://github.com/microsoft/PowerToys/pull/30727) | Improve installation workflow |
| [#30745](https://github.com/microsoft/PowerToys/pull/30745) | Log + runtime error handling in `GetFeedback` (C# predictor): init logger, try/catch, graceful `FeedbackItem` |
| [#30759](https://github.com/microsoft/PowerToys/pull/30759) | Disable on ARM64 (no PS 7.4 MSI yet) |
| [#32034](https://github.com/microsoft/PowerToys/pull/32034) | Support PowerShell Preview installation |
| [#32766](https://github.com/microsoft/PowerToys/pull/32766) | Upgrade to PSGallery release + support ARM64 |
| [#32892](https://github.com/microsoft/PowerToys/pull/32892) | Fix CmdNotFound page init on ARM |
| [#37690](https://github.com/microsoft/PowerToys/pull/37690) | Only enable experimental features if they exist |
| [#44639](https://github.com/microsoft/PowerToys/pull/44639) | Repo-wide `$(RepoRoot)` path convention (touched module project) |

## Excluded as noise (not distilled)
- Cross-cutting build/deps PRs that merely swept the module: CppWinRT bumps
  ([#45420](https://github.com/microsoft/PowerToys/pull/45420),
  [#31396](https://github.com/microsoft/PowerToys/pull/31396)), PCH unification
  ([#31055](https://github.com/microsoft/PowerToys/pull/31055)), VS 2026 support
  ([#44304](https://github.com/microsoft/PowerToys/pull/44304)) — their review threads (IAsyncAction
  `.get()`, coroutine ABI, toolset conditionals) are general C++/WinRT concerns, not CmdNotFound
  logic. The one durable carry-over is the `$(RepoRoot)` path rule (kept above).
- `/azp run`, "LGTM", "Amazing work", merge-conflict coordination, and other CI/process chatter.
