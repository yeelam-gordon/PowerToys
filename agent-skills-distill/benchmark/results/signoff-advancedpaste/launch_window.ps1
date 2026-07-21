# Summon the AdvancedPaste window WITHOUT the global hotkey by impersonating the
# PowerToys Runner side of its named-pipe protocol (App.xaml.cs OnLaunched ->
# ProcessNamedPipe(arg2); NamedPipeProcessor reads UTF-16 lines; "ShowUI" ->
# ShowWindow()). This proves whether the window is launchable directly.
$ErrorActionPreference = "Stop"
$exe = "C:\s\PowerToys\x64\Release\WinUI3Apps\PowerToys.AdvancedPaste.exe"
$pipeName = "advancedpaste_signoff_" + (Get-Random)

$server = New-Object System.IO.Pipes.NamedPipeServerStream(
    $pipeName, [System.IO.Pipes.PipeDirection]::Out, 1,
    [System.IO.Pipes.PipeTransmissionMode]::Byte,
    [System.IO.Pipes.PipeOptions]::Asynchronous)

# arg1 = a live PID to watch (use our own so the app stays up), arg2 = pipe name
$proc = Start-Process -FilePath $exe -ArgumentList @("$PID", $pipeName) -PassThru
Write-Host "launched AdvancedPaste pid=$($proc.Id) pipe=$pipeName"

$server.WaitForConnection()
Write-Host "client connected"
Start-Sleep -Milliseconds 500

$bytes = [System.Text.Encoding]::Unicode.GetBytes("ShowUI`r`n")
$server.Write($bytes, 0, $bytes.Length)
$server.Flush()
Write-Host "sent ShowUI"

# keep pipe + this process alive so AdvancedPaste keeps running for inspection
Start-Sleep -Seconds 600
