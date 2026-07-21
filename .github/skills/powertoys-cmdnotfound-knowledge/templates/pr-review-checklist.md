# CmdNotFound PR Review Checklist

Apply **after** reading the diff cold (see anti-anchoring in SKILL.md). Only check rows whose files
the diff actually touches.

## Profile & install safety
- [ ] No `New-Item $PROFILE` / `$PROFILE` mutation on the **requirements-check** path — only on
  explicit install/uninstall (regression #32508, #42365).
- [ ] `Install-Module` used only when the module is absent; upgrades use `Update-Module` (#32766).
- [ ] Legacy marker `34de4b3d-13a8-4540-b76d-b9e8d3851756` detection + upgrade branch preserved;
  current marker `f45873b3-b655-43a6-b217-97c00aa0db58` unchanged.

## Script ↔ C# contract
- [ ] Every edited `Write-Host` status line still matches the corresponding
  `CmdNotFoundViewModel` `result.Contains("…")` check (and vice versa).
- [ ] Compared status strings remain invariant **English** (not localized).
- [ ] Color/encoding kept clean (`NO_COLOR=1`); no ANSI codes leaking into compared stdout (#37663, #34856).

## Reliability
- [ ] No suggestion/network lookup can throw into pwsh startup; offline/DNS-failure fails silent
  (#33065, #33061, #34286).
- [ ] `Enable-ExperimentalFeature` guarded by a `Get-ExperimentalFeature`-contains check (#37690).
- [ ] WinGet.Client version floor `1.8.1133` consistent across all three scripts.

## Threading & UX
- [ ] No new synchronous `pwsh.exe` `Process.Start`+`ReadLine` on the UI thread / view-model ctor (#38197).
- [ ] `pwsh-preview.cmd` fallback and PATH rebuild (Machine+User) preserved.

## Platform, GPO & build
- [ ] ARM64 and PS-Preview-only setups considered when touching bootstrapping (#30759, #32034, #32892).
- [ ] Native module honors `getConfiguredCmdNotFoundEnabledValue()` at construction; register/unregister idempotent.
- [ ] GPO/telemetry (`Trace::EnableCmdNotFoundGpo`, `CmdNotFoundInstall/UninstallEvent`) wired for new enable/disable paths.
- [ ] Project files use `$(RepoRoot)`, not `..\..\` relative paths (#44639).
