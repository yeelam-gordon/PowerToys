param(
    [Parameter(Mandatory)][string]$HarnessPath,
    [Parameter(Mandatory)][string]$RustCliPath,
    [Parameter(Mandatory)][string]$RustSettingsPath,
    [Parameter(Mandatory)][string]$ProbePath,
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidateRange(1, 10)][int]$Rounds = 3
)

$ErrorActionPreference = 'Stop'
foreach ($path in @($HarnessPath, $RustCliPath, $RustSettingsPath, $ProbePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing benchmark executable: $path"
    }
}
if (Get-Process | Where-Object { $_.ProcessName -in @('PowerToys.Awake', 'awake', 'PowerToys.Awake.LeanProbe') }) {
    throw 'Close other Awake instances before running this benchmark.'
}

$output = [Collections.Generic.List[string]]::new()
foreach ($workload in @('CLI', 'Settings')) {
    foreach ($round in 1..$Rounds) {
        $labels = if ($round % 2 -eq 0) { @('Rust', 'LeanCSharp') } else { @('LeanCSharp', 'Rust') }
        foreach ($label in $labels) {
            $executable = if ($label -eq 'LeanCSharp') {
                $ProbePath
            } elseif ($workload -eq 'CLI') {
                $RustCliPath
            } else {
                $RustSettingsPath
            }
            $arguments = if ($workload -eq 'CLI') {
                @('for', '5m', '--pid', '{pid}')
            } elseif ($label -eq 'LeanCSharp') {
                @('--settings', '{settings}', '--pid', '{pid}')
            } else {
                @('--use-pt-config', '--pid', '{pid}')
            }

            $startInfo = [Diagnostics.ProcessStartInfo]::new($HarnessPath)
            $startInfo.UseShellExecute = $false
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            foreach ($argument in @("$label-$workload-$round", $executable) + $arguments) {
                $startInfo.ArgumentList.Add($argument)
            }
            $process = [Diagnostics.Process]::Start($startInfo)
            $startedAt = $process.StartTime.ToUniversalTime()
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit(180000)) {
                # Record only descendants belonging to this launch, not processes sharing a name.
                $snapshot = @(Get-CimInstance Win32_Process)
                $owned = [Collections.Generic.HashSet[uint32]]::new()
                $owned.Add([uint32]$process.Id) | Out-Null
                do {
                    $added = $false
                    foreach ($candidate in $snapshot) {
                        if ($owned.Contains([uint32]$candidate.ParentProcessId) -and
                            $candidate.CreationDate.ToUniversalTime() -ge $startedAt -and
                            $owned.Add([uint32]$candidate.ProcessId)) {
                            $added = $true
                        }
                    }
                } while ($added)
                $process.CloseMainWindow() | Out-Null
                $process.WaitForExit(2000) | Out-Null
                foreach ($ownedId in $owned) {
                    $candidate = Get-Process -Id $ownedId -ErrorAction SilentlyContinue
                    if ($candidate -and $candidate.StartTime.ToUniversalTime() -ge $startedAt) {
                        Stop-Process -Id $ownedId -Force
                    }
                }
                throw "Benchmark timeout after 180s: PID=$($process.Id); label=$label; workload=$workload; owned PIDs=$($owned -join ',')"
            }
            $text = $stdout.GetAwaiter().GetResult()
            $errors = $stderr.GetAwaiter().GetResult()
            if ($process.ExitCode -ne 0) {
                throw "Benchmark failed with exit code $($process.ExitCode): $errors"
            }
            Write-Output $text.TrimEnd()
            $output.Add($text.TrimEnd())
            $process.Dispose()
        }
    }
}

$output | Set-Content -LiteralPath $OutputPath -Encoding utf8
