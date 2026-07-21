# Verifies synthetic input (SendInput) is currently working by injecting a key
# event and checking the injected-event count. Exits 0 if working, 1 if blocked.
. (Join-Path $PSScriptRoot "input_helpers.ps1")
$down = [WinInput]::Key(0x10,$false)
$up   = [WinInput]::Key(0x10,$true)
$fg = [WinInput]::GetForegroundWindow()
Write-Host "SendInput down=$down up=$up  GetForegroundWindow=$fg  time=$(Get-Date -Format HH:mm:ss)"
if ($down -gt 0 -and $up -gt 0) { Write-Host "INPUT: OK"; exit 0 } else { Write-Host "INPUT: BLOCKED (down=$down up=$up)"; exit 1 }
