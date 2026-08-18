# Long-lived AdvancedPaste controller.
# Impersonates the PowerToys Runner named-pipe server so the AP window can be
# summoned (ShowUI) WITHOUT the global hotkey. Stays alive and re-shows the
# window whenever a trigger file appears, so a single AP process can be driven
# through many sign-off iterations.
param(
    # Environment-specific — no portable default. Pass -Exe or set $env:POWERTOYS_AP_EXE.
    [string]$Exe  = $env:POWERTOYS_AP_EXE,
    [string]$WorkDir = $(if ($env:AP_SIGNOFF_WORKDIR) { $env:AP_SIGNOFF_WORKDIR } else { Join-Path $env:TEMP 'ap-signoff-work' })
)
$ErrorActionPreference = "Stop"
if (-not $Exe -or -not (Test-Path $Exe)) {
    throw "AdvancedPaste exe not found. Pass -Exe <path to PowerToys.AdvancedPaste.exe> or set `$env:POWERTOYS_AP_EXE. This sign-off runner is environment-specific and ships no machine-path default."
}
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$pipeName = "advancedpaste_acc_" + (Get-Random)
$trigger  = Join-Path $WorkDir "show.trigger"
$pidFile  = Join-Path $WorkDir "ap.pid"
$readyFile= Join-Path $WorkDir "controller.ready"
Remove-Item $trigger,$readyFile -ErrorAction SilentlyContinue

$server = New-Object System.IO.Pipes.NamedPipeServerStream(
    $pipeName, [System.IO.Pipes.PipeDirection]::Out, 1,
    [System.IO.Pipes.PipeTransmissionMode]::Byte,
    [System.IO.Pipes.PipeOptions]::Asynchronous)

$proc = Start-Process -FilePath $Exe -ArgumentList @("$PID", $pipeName) -PassThru
Set-Content -Path $pidFile -Value $proc.Id
Write-Host "launched AdvancedPaste pid=$($proc.Id) pipe=$pipeName"

$connectTask = $server.WaitForConnectionAsync()
$timeoutMs = 30000
if (-not $connectTask.Wait($timeoutMs)) {
    if ($proc.HasExited) { throw "AdvancedPaste (pid=$($proc.Id)) exited with code $($proc.ExitCode) before connecting to the named pipe — check the -Exe path / build." }
    throw "Timed out after $([int]($timeoutMs/1000))s waiting for AdvancedPaste (pid=$($proc.Id)) to connect to the named pipe. The process is still running; verify the build responds to ShowUI."
}
Write-Host "client connected"
Start-Sleep -Milliseconds 800
$bytes = [System.Text.Encoding]::Unicode.GetBytes("ShowUI`r`n")
$server.Write($bytes, 0, $bytes.Length); $server.Flush()
Write-Host "sent initial ShowUI"
Set-Content -Path $readyFile -Value "ready"

# Re-show loop: whenever the trigger file appears, send another ShowUI.
# Exit if the AdvancedPaste process it drives has gone away so we don't spin forever.
while ($true) {
    if ($proc.HasExited) {
        Write-Host "AdvancedPaste (pid=$($proc.Id)) exited with code $($proc.ExitCode); controller stopping."
        break
    }
    if (Test-Path $trigger) {
        Remove-Item $trigger -ErrorAction SilentlyContinue
        try {
            $server.Write($bytes, 0, $bytes.Length); $server.Flush()
            Write-Host "sent ShowUI (re-show)"
        } catch {
            Write-Host "pipe write failed: $_"
            break
        }
    }
    Start-Sleep -Milliseconds 200
}
