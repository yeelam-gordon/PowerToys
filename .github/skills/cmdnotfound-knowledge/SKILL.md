---
name: cmdnotfound-knowledge
description: 'PowerToys Command Not Found (CmdNotFound) module knowledge: feature->file/function map, recurring regression playbooks (offline/DNS pwsh startup crash, empty $PROFILE creation, install on the UI thread, garbled/localized install logs, legacy bundled module -> PowerShell Gallery upgrade, WinGet.Client version floor, ARM64 gaps, GPO gating), maintainer review rules, and Pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/cmdNotFound and the CmdNotFound Settings page/scripts — PowerShell 7 feedback provider, WinGet suggestion, $PROFILE modification, module install/uninstall/upgrade, experimental features, GPO. Keywords: Command Not Found, CmdNotFound, PowerShell feedback provider, WinGet, Microsoft.WinGet.CommandNotFound, $PROFILE, Install-Module, PSFeedbackProvider, pwsh, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Command Not Found Knowledge

Grounded engineering knowledge for the PowerToys **Command Not Found (CmdNotFound)** module — a
PowerShell 7.4+ feedback provider that detects a failed command at the prompt and suggests a WinGet
package to install. The feature is not a running process: it is a **PowerShell module registered in
the user's `$PROFILE`**, installed/managed from the PowerToys Settings page via bundled `.ps1`
scripts, and gated by a native module-interface DLL + GPO. Use this to localize code fast, avoid the
recurring install/profile/offline traps, and enforce conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/cmdNotFound/` or the CmdNotFound Settings
  page / bundled scripts (`Assets/Settings/Scripts/*.ps1`).
- Fixing/triaging a CmdNotFound bug: pwsh crashes/hangs on startup (esp. offline / no DNS), Settings
  page freezes while checking requirements, `$PROFILE` created/modified unexpectedly, garbled or
  localized install output, module install/upgrade failing, ARM64 unsupported.
- Reviewing a CmdNotFound PR against maintainer conventions and regression traps.
- Touching profile registration, WinGet.Client / Microsoft.WinGet.CommandNotFound install logic,
  experimental-feature enabling, or GPO/telemetry wiring.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring).
Source root: [`src/modules/cmdNotFound/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdNotFound)
plus the Settings-UI page + scripts (the actual install logic lives there, not under the module dir).

| Sub-feature | Implementation (file · symbol) |
|---|---|
| Native module lifecycle (enable/disable on load) | `CmdNotFoundModuleInterface/dllmain.cpp` `CmdNotFound` ctor, `install_module()`, `uninstall_module()` |
| GPO gate (enabled/disabled policy) | `dllmain.cpp` `gpo_policy_enabled_configuration()` -> `powertoys_gpo::getConfiguredCmdNotFoundEnabledValue`; managed-VM side `CmdNotFoundViewModel.InitializeEnabledValue` -> `GPOWrapper.GetConfiguredCmdNotFoundEnabledValue()` |
| Native install/uninstall shell-out | `dllmain.cpp` builds `pwsh.exe … -File …\WinUI3Apps\Assets\Settings\Scripts\EnableModule.ps1 -scriptPath …` via `system()` |
| ETW telemetry (GPO enable/disable, install/uninstall) | `CmdNotFoundModuleInterface/trace.cpp` `Trace::EnableCmdNotFoundGpo`; managed `CmdNotFoundInstallEvent` / `CmdNotFoundUninstallEvent` |
| Settings page view-model (all button commands) | `settings-ui/Settings.UI/ViewModels/CmdNotFoundViewModel.cs` |
| Requirement detection (PS 7.4, WinGet.Client, profile state) | `CheckCmdNotFoundRequirements.ps1` + `CmdNotFoundViewModel.CheckCommandNotFoundRequirements()` (string-matches script output) |
| Register module in `$PROFILE` (install + legacy upgrade) | `Assets/Settings/Scripts/EnableModule.ps1` |
| Remove module from `$PROFILE` (uninstall) | `Assets/Settings/Scripts/DisableModule.ps1` |
| Install/update WinGet.Client module | `Assets/Settings/Scripts/InstallWinGetClientModule.ps1` |
| Install PowerShell 7 (+ WinGet/VCLibs bootstrapping) | `Assets/Settings/Scripts/InstallPowerShell7.ps1` |
| PowerShell process runner (stdout capture, PS-preview fallback) | `CmdNotFoundViewModel.RunPowerShellScript` / `RunPowerShellOrPreviewScript` |
| Settings XAML page + OOBE | `SettingsXAML/Views/CmdNotFoundPage.xaml{,.cs}`, `SettingsXAML/OOBE/Views/OobeCmdNotFound.xaml{,.cs}` |
| Persisted settings (versioned, minimal) | `settings-ui/Settings.UI.Library/CmdNotFoundSettings.cs` |
| **Feedback/prediction provider (suggests `winget install`)** — historical, now PSGallery | `WinGetCommandNotFoundFeedbackPredictor.cs` `GetFeedback` (calls `FindPackages`, builds candidates; must `try/catch` + `Logger.LogError` and return a graceful `FeedbackItem`, PR #30745), ctor `Logger.InitializeLogger("\\CmdNotFound\\Logs")`. Was under `src/modules/cmdNotFound/CmdNotFound/`; moved out of repo in [PR #32766](https://github.com/microsoft/PowerToys/pull/32766) |

### Two module identities you MUST NOT confuse
- **`f45873b3-b655-43a6-b217-97c00aa0db58`** — the **current** marker written to `$PROFILE`; registers
  `Import-Module -Name Microsoft.WinGet.CommandNotFound` (the PowerShell Gallery module).
- **`34de4b3d-13a8-4540-b76d-b9e8d3851756`** — the **legacy** marker; registered a PowerToys-bundled
  `Import-Module "$scriptPath\WinGetCommandNotFound.psd1"`. `EnableModule.ps1` detects it and
  *upgrades* the profile in place. `CheckCmdNotFoundRequirements.ps1` reports it as "Outdated".
  Both GUIDs are string-searched in `EnableModule.ps1` / `DisableModule.ps1` — treat them as a
  contract, never re-generate them (see [PR #32766](https://github.com/microsoft/PowerToys/pull/32766)).

### Script<->C# output-string contract (critical)
`CmdNotFoundViewModel` decides UI state purely by `result.Contains("…")` on the scripts' `Write-Host`
lines (e.g. `"PowerShell 7.4 or greater detected."`, `"WinGet Client module detected."`,
`"Command Not Found module is registered in the profile file."`, `"Module was successfully upgraded
in the profile file."`). Each such line in the `.ps1` carries the comment *"This message will be
compared against in Command Not Found Settings page code behind."* **Editing the wording on either
side without the other silently breaks detection.**

## Regression Playbooks

Rule by rule: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### pwsh crashes / hangs on startup when offline or DNS fails
- **Symptom:** after installing CmdNotFound, PowerShell 7 crashes, throws, or stalls at every
  startup when there is no internet / DNS is unreachable (corp intranet, offline machines).
- **Where:** the registered feedback provider runs on `Import-Module` at profile load
  (`WinGetCommandNotFoundFeedbackPredictor.GetFeedback`, historically under
  `src/modules/cmdNotFound/CmdNotFound/`).
- **Root cause:** the suggestion path performs a network/WinGet lookup and unhandled exceptions
  propagate into pwsh startup.
- **Guardrail:** never let a suggestion lookup throw into the prompt; fail closed and silent when
  offline. Evidence: issues [#33065](https://github.com/microsoft/PowerToys/issues/33065),
  [#33061](https://github.com/microsoft/PowerToys/issues/33061),
  [#34286](https://github.com/microsoft/PowerToys/issues/34286),
  [#33251](https://github.com/microsoft/PowerToys/issues/33251),
  [#33304](https://github.com/microsoft/PowerToys/issues/33304). Much of the fix shipped by
  migrating to the maintained PSGallery module ([#32766](https://github.com/microsoft/PowerToys/pull/32766),
  [#33669](https://github.com/microsoft/PowerToys/issues/33669)).

### GetFeedback exception breaks the predictor silently (no logs, no message)
- **Symptom:** the feedback predictor produces no suggestion and gives no diagnostic when it fails
  (notably when WinGet COM isn't usable — e.g. PowerShell 7 installed from the **Store/MSIX**), and
  there is no log to explain why.
- **Where:** `WinGetCommandNotFoundFeedbackPredictor.cs` — the `GetFeedback(FeedbackContext context,
  CancellationToken token)` method (which calls `FindPackages` and builds the `winget install --id …`
  candidate list) and the `WinGetCommandNotFoundFeedbackPredictor(string guid)` constructor.
  (Historical path `src/modules/cmdNotFound/CmdNotFound/`; the C# module later moved to the PSGallery
  release in [PR #32766](https://github.com/microsoft/PowerToys/pull/32766), so it is no longer in-repo.)
- **Root cause:** `GetFeedback` ran `FindPackages`/candidate-building with **no exception handling**
  and **no logger initialized**, so any runtime failure (WinGet not available, COM error) threw
  uncaught and vanished with no trace.
- **Guardrail (as shipped by [PR #30745](https://github.com/microsoft/PowerToys/pull/30745)):**
  (1) initialize the file logger once in the constructor —
  `Logger.InitializeLogger("\\CmdNotFound\\Logs")`; (2) wrap the `GetFeedback` body in `try/catch`,
  and on `catch (Exception ex)` call `Logger.LogError("GetFeedback failed to execute", ex)` and
  **return a user-facing `FeedbackItem`** explaining the known PS7 Store/MSIX limitation instead of
  letting the exception propagate. Rule: a `Feedback`/`Prediction` subsystem callback must never
  throw into the pwsh prompt — log and return a graceful `FeedbackItem`.

### Requirements check creates `$PROFILE` before the user opts in
- **Symptom:** an empty `$PROFILE` and a `Documents\PowerShell` folder appear even though the user
  never enabled CmdNotFound, changing Explorer address-bar `powershell` behavior.
- **Where:** `CheckCmdNotFoundRequirements.ps1` and `EnableModule.ps1` both do
  `if (!(Test-Path $PROFILE)) { New-Item -Path $PROFILE -ItemType File }`.
- **Root cause:** the *requirements check* (run automatically when the page opens) creates the
  profile before the user opts in.
- **Guardrail:** only create/modify `$PROFILE` on an explicit install action; the check path must be
  read-only. Evidence: issues [#32508](https://github.com/microsoft/PowerToys/issues/32508),
  [#42365](https://github.com/microsoft/PowerToys/issues/42365).

### Settings page freezes while checking requirements / installing
- **Symptom:** the CmdNotFound page (or OOBE) hangs for seconds; "installing on the UI thread";
  slow/blocked UI while `Install-Module` runs.
- **Where:** `CmdNotFoundViewModel.InitializeEnabledValue()` (ctor) calls
  `CheckCommandNotFoundRequirements()`, which runs `pwsh.exe` synchronously via
  `RunPowerShellScript` and blocks reading `StandardOutput` line by line on the calling thread.
- **Root cause:** synchronous `Process.Start` + `ReadLine` loop on the UI thread; module install can
  take a long time.
- **Guardrail:** run script invocations off the UI thread and reflect progress asynchronously; never
  add new synchronous `pwsh.exe` calls to the ctor/property path. Evidence: issues
  [#38197](https://github.com/microsoft/PowerToys/issues/38197),
  [#33179](https://github.com/microsoft/PowerToys/issues/33179),
  [#33178](https://github.com/microsoft/PowerToys/issues/33178).

### Garbled / localized install output breaks detection
- **Symptom:** "weird characters" during install; on non-English (e.g. Chinese) systems the install
  log is garbled and requirement detection misfires.
- **Where:** `RunPowerShellScript` sets `NO_COLOR=1` and reads stdout; detection relies on **exact
  English** substrings in script output.
- **Root cause:** ANSI color codes / console encoding pollute stdout, and localized/culture-shifted
  output no longer matches the hard-coded English `Contains(...)` checks.
- **Guardrail:** keep the emitted status lines invariant English (they are a contract, not UI text)
  and strip color; don't localize the compared `Write-Host` strings. Evidence: issues
  [#37663](https://github.com/microsoft/PowerToys/issues/37663),
  [#34856](https://github.com/microsoft/PowerToys/issues/34856).

### Enabling experimental features fails on clean/older PowerShell
- **Symptom:** `EnableModule.ps1` errors calling `Enable-ExperimentalFeature` for a feature the
  installed PowerShell doesn't have.
- **Where:** `EnableModule.ps1` top — `PSFeedbackProvider` / `PSCommandNotFoundSuggestion`.
- **Root cause:** feature names differ across PS versions; enabling a nonexistent feature throws.
- **Guardrail:** guard every `Enable-ExperimentalFeature` with a
  `Get-ExperimentalFeature | … -contains` check (present in current source). Evidence:
  [PR #37690](https://github.com/microsoft/PowerToys/pull/37690).

### ARM64 / PowerShell-Preview environment gaps
- **Symptom:** CmdNotFound unavailable or non-functional on ARM64; PowerShell 7 Preview not detected.
- **Where:** `InstallPowerShell7.ps1` (arch detection, no PS 7.4 MSI for arm64 originally);
  `CmdNotFoundViewModel.RunPowerShellOrPreviewScript` / `pwsh-preview.cmd` discovery on `PATH`.
- **Root cause:** platform coverage gaps in bootstrapping and PS-preview path resolution.
- **Guardrail:** verify install/requirement flows on ARM64 and with PS-Preview-only setups when
  touching bootstrapping. Evidence: [PR #30759](https://github.com/microsoft/PowerToys/pull/30759)
  (disable on ARM64), later re-enabled in [#32766](https://github.com/microsoft/PowerToys/pull/32766);
  PS-Preview support [#32034](https://github.com/microsoft/PowerToys/pull/32034); ARM64 page-init fix
  [#32892](https://github.com/microsoft/PowerToys/pull/32892).

## Review Rules

Enforce these when reviewing or authoring CmdNotFound changes:

- **Never let a suggestion lookup throw into pwsh startup.** Offline/DNS-failure must fail silently,
  not crash the shell — this is the module's single most frequent regression (#33065, #33061, #34286).
- **Keep the script↔C# status strings byte-for-byte in sync.** Any edit to a compared `Write-Host`
  line in the scripts must be mirrored in `CmdNotFoundViewModel` `Contains(...)` checks (and vice
  versa); these strings are an API, and must stay invariant English.
- **Only mutate `$PROFILE` on explicit install/uninstall.** The requirements-check path must be
  read-only; do not `New-Item $PROFILE` during detection (#32508, #42365).
- **Run `pwsh.exe` off the UI thread.** Don't add synchronous `Process.Start`+`ReadLine` work to
  view-model constructors or property getters (#38197).
- **Guard experimental-feature enabling.** Wrap `Enable-ExperimentalFeature` in a
  `Get-ExperimentalFeature`-contains check ([#37690](https://github.com/microsoft/PowerToys/pull/37690)).
- **Respect the WinGet.Client version floor `1.8.1133`.** Detection, install, and enable scripts all
  gate on it; keep the constant consistent across `CheckCmdNotFoundRequirements.ps1`,
  `InstallWinGetClientModule.ps1`, and `EnableModule.ps1`.
- **Preserve both profile-marker GUIDs and the legacy→PSGallery upgrade branch.** Don't drop the
  `34de4b3d…` detection — users still have old profiles to upgrade (#32766).
- **Honor GPO at construction.** The native module installs/uninstalls based on
  `getConfiguredCmdNotFoundEnabledValue()` in its ctor; keep register/unregister idempotent so a
  policy flip cleans the profile.
- **Use `$(RepoRoot)` in project files, not `..\..\`.** Repo-wide convention reaffirmed in
  [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) (adds a leading `..` = wrong path).
- **`Update-Module` to upgrade, `Install-Module` only when absent.** Conflating them was a real
  install-workflow bug ([#32766](https://github.com/microsoft/PowerToys/pull/32766) discussion).

## Pitfalls

- **Never** treat CmdNotFound as a live process — it is a `$PROFILE` `Import-Module` entry executed by
  every new pwsh session; a bad module or throw at load time breaks the user's shell globally.
- **Never** edit a `Write-Host` line that carries the *"compared against in … Settings page code
  behind"* comment without updating the matching C# `Contains(...)` — detection silently breaks.
- **Never** run requirement detection or install synchronously on the UI thread — page load already
  shells out to `pwsh.exe` and can hang for seconds (#38197).
- **Never** create `$PROFILE` just to inspect it — that alone regressed Explorer address-bar behavior
  (#42365) and littered profiles for users who never opted in (#32508).
- **Don't localize the status strings** or leave ANSI color in stdout — both corrupt the
  English-substring contract on non-English consoles (#34856, #37663). `NO_COLOR=1` is set for this.
- **The suggestion module is external** (`Microsoft.WinGet.CommandNotFound` on the PowerShell Gallery,
  built/signed in its own repo) — PowerToys only registers/installs it; fixes to suggestion logic
  itself live outside this repo (conversation on #32766).
- **`pwsh.exe` may not be on PATH yet** after PS7 install; the view-model rebuilds the process `PATH`
  from Machine+User and also probes for `pwsh-preview.cmd` — preserve both fallbacks.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a CmdNotFound PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source: [`src/modules/cmdNotFound/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdNotFound) ·
  [Settings scripts](https://github.com/microsoft/PowerToys/tree/main/src/settings-ui/Settings.UI/Assets/Settings/Scripts) ·
  [`CmdNotFoundViewModel.cs`](https://github.com/microsoft/PowerToys/blob/main/src/settings-ui/Settings.UI/ViewModels/CmdNotFoundViewModel.cs)
- [Microsoft.WinGet.CommandNotFound (PowerShell Gallery)](https://www.powershellgallery.com/packages/Microsoft.WinGet.CommandNotFound) ·
  [Install PowerShell on Windows](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows)
