# CmdNotFound Bug Triage — Symptom → Likely File/Function

Use the Module Map in SKILL.md to confirm in source. Most user-facing bugs live in the **scripts**,
not the native module. Treat entries as hypotheses, not ground truth.

| Symptom | Start here | Notes / evidence |
|---|---|---|
| pwsh crashes/hangs on startup, esp. offline / no DNS | registered `Import-Module` in `$PROFILE`; native error handling `dllmain.cpp`, `trace.cpp` | Suggestion lookup throwing into startup. #33065, #33061, #34286, #33251, #33304 |
| Empty `$PROFILE` / `Documents\PowerShell` appears without opt-in | `CheckCmdNotFoundRequirements.ps1` `Test-Path $PROFILE`/`New-Item`; also `EnableModule.ps1` | Check path must be read-only. #32508, #42365 |
| Settings page / OOBE freezes; "installs on UI thread" | `CmdNotFoundViewModel.InitializeEnabledValue` → `CheckCommandNotFoundRequirements` → `RunPowerShellScript` (sync stdout read) | Move off UI thread. #38197, #33179, #33178 |
| Garbled chars / wrong detection on non-English systems | `RunPowerShellScript` (`NO_COLOR`), status-string `Contains(...)` checks vs script `Write-Host` | Encoding + English-substring contract. #37663, #34856 |
| Requirement state wrong after a script wording change | mismatch between `.ps1` `Write-Host` and `CmdNotFoundViewModel` `Contains(...)` | The compared-string contract broke |
| Install/upgrade does nothing or fails | `EnableModule.ps1` (Install vs Update-Module; `-scriptPath` arg), installer custom action | #32766 (custom action wasn't passed install folder) |
| "Outdated version" reported; old bundled module | legacy GUID `34de4b3d…` branch in `EnableModule.ps1`/`CheckCmdNotFoundRequirements.ps1` | Legacy→PSGallery upgrade. #33669, #32766 |
| WinGet.Client install error / "untrusted repository" | `InstallWinGetClientModule.ps1`, version floor `1.8.1133` | #31914, #31378 |
| Not working on ARM64 / PS Preview | `InstallPowerShell7.ps1` (arch), `RunPowerShellOrPreviewScript` (`pwsh-preview.cmd`) | #30759, #32034, #32892 |
| Module missing entirely / won't install (Store build) | native `install_module()` shell-out path; packaged write permissions | #31935, #36494 |
| Enable-ExperimentalFeature errors | `EnableModule.ps1` top guard | #37690 |
