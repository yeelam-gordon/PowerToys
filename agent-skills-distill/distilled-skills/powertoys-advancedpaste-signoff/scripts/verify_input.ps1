# Verifies synthetic input (SendInput) is currently working by injecting a key
# event and checking the injected-event count. Exits 0 if working, 1 if blocked.
. (Join-Path $PSScriptRoot "input_helpers.ps1")
$n = [WinInput]::Key(0x10,$false); [WinInput]::Key(0x10,$true) | Out-Null
$fg = [WinInput]::GetForegroundWindow()
Write-Host "SendInput events=$n  GetForegroundWindow=$fg  time=$(Get-Date -Format HH:mm:ss)"
if ($n -gt 0) { Write-Host "INPUT: OK"; exit 0 } else { Write-Host "INPUT: BLOCKED"; exit 1 }
