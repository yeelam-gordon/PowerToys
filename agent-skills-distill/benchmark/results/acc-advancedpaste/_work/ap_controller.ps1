# Long-lived AdvancedPaste controller.
# Impersonates the PowerToys Runner named-pipe server so the AP window can be
# summoned (ShowUI) WITHOUT the global hotkey. Stays alive and re-shows the
# window whenever a trigger file appears, so a single AP process can be driven
# through many sign-off iterations.
param(
    [string]$Exe  = "C:\s\PowerToys\x64\Release\WinUI3Apps\PowerToys.AdvancedPaste.exe",
    [string]$WorkDir = "C:\s\Demo\SkillForDistill\benchmark\results\acc-advancedpaste\_work"
)
$ErrorActionPreference = "Stop"
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

$server.WaitForConnection()
Write-Host "client connected"
Start-Sleep -Milliseconds 800
$bytes = [System.Text.Encoding]::Unicode.GetBytes("ShowUI`r`n")
$server.Write($bytes, 0, $bytes.Length); $server.Flush()
Write-Host "sent initial ShowUI"
Set-Content -Path $readyFile -Value "ready"

# Re-show loop: whenever the trigger file appears, send another ShowUI.
while ($true) {
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
