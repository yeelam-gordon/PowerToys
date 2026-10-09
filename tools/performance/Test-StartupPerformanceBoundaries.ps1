<#
.SYNOPSIS
Tests startup benchmark boundaries with memory-only process and filesystem mocks.
.DESCRIPTION
Extracts actual functions and the finalizer without executing the measurement script or its
native helpers. Requires no test framework; supports Windows PowerShell 5.1 and PowerShell 7.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$sourcePath = Join-Path $PSScriptRoot 'Measure-StartupPerformance.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($sourcePath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0)
{
    throw ($parseErrors | Out-String)
}

foreach ($name in 'Get-RootProcesses', 'Resolve-OutputDirectory', 'Test-LogFile', 'Restore-DataFolder',
    'Stop-Processes', 'Wait-PinnedProcesses', 'Stop-ProcessAndDescendants', 'Stop-RootProcesses',
    'Stop-LaunchedProcesses', 'Stop-Runner', 'Stop-AllRunners', 'Close-Settings')
{
    $definition = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    if ($null -eq $definition)
    {
        throw "Missing production function: $name"
    }

    # Test-only type adaptation lets controlled objects exercise the unchanged stopping bodies.
    # Native discovery/window calls are adapted to memory-only seams, not entire stopping helpers.
    $text = $definition.Extent.Text.Replace('[Diagnostics.Process', '[object').Replace('[PowerToysPerformance.PinnedProcess', '[object')
    $text = $text.Replace('[PowerToysPerformance.ProcessTree]::GetDescendants($Process.Id, $Process.StartTime.ToUniversalTime())', '(Get-TestDescendants)')
    $text = $text.Replace("[PowerToysPerformance.WindowFinder]::FindTopLevelWindow(`$Process.Id, 'PToyTrayIconWindow')", '(Get-TestTrayWindow)')
    $text = $text.Replace('[PowerToysPerformance.WindowFinder]::PostClose($trayWindow)', '(Send-TestTrayClose)')
    . ([scriptblock]::Create($text))
}

$run = @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })[-1]
$finalizer = [scriptblock]::Create(($run.Finally.Statements | ForEach-Object { $_.Extent.Text }) -join "`n")
$script:assertions = 0
function Assert
{
    param([bool]$Condition, [string]$Message)
    if (-not $Condition)
    {
        throw "FAIL: $Message"
    }

    $script:assertions++
    Write-Host "PASS: $Message"
}

foreach ($file in 'Measure-StartupPerformance.ps1', 'Compare-StartupPerformance.ps1', 'Test-StartupPerformanceBoundaries.ps1')
{
    $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file), [ref]$tokens, [ref]$parseErrors)
    Assert ($parseErrors.Count -eq 0) "Static parse: $file"
}
$profile = [xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PowerToys.Performance.wprp') -Raw)
Assert ($profile.WindowsPerformanceRecorder.Profiles.EventProvider.Name -eq '9d83a68b-e53f-5d64-0e80-e3a9faf69485') 'WPR XML parses and retains performance provider identity'

# This is a source-order check: the full benchmark must never run in this suite.
$preflight = @($ast.EndBlock.Statements | Where-Object { $_.Extent.Text -like '$OutputDirectory = Resolve-OutputDirectory*' })[0]
$workAssignment = @($ast.EndBlock.Statements | Where-Object { $_.Extent.Text -like '$workFolder = Join-Path*' })[0]
$firstWrite = @($ast.FindAll({
    param($node)
    if ($node -isnot [Management.Automation.Language.CommandAst] -or $node.GetCommandName() -notin 'New-Item', 'New-SampleFiles', 'Stop-AllRunners', 'Save-DataFolder')
    {
        return $false
    }
    $parent = $node.Parent
    while ($null -ne $parent)
    {
        if ($parent -is [Management.Automation.Language.FunctionDefinitionAst]) { return $false }
        $parent = $parent.Parent
    }
    return $true
}, $true) | Sort-Object { $_.Extent.StartOffset })[0]
Assert ($null -ne $preflight -and $preflight.Extent.StartOffset -lt $firstWrite.Extent.StartOffset) 'Output validation precedes writes, snapshot, and takeover'
Assert ($workAssignment.Extent.StartOffset -gt $preflight.Extent.StartOffset) 'Work folder uses normalized output'

$dataFolder = 'C:\fake-data\PowerToys'
foreach ($path in $dataFolder, ($dataFolder + '\'), 'c:\FAKE-DATA\POWERTOYS\out', ($dataFolder + '\out\..\out'))
{
    $rejected = $false
    try { $null = Resolve-OutputDirectory -Directory $path -DataFolder $dataFolder -TakeOver $true }
    catch { $rejected = $_.Exception.Message -like 'OutputDirectory must be outside*' }
    Assert $rejected "Reject protected output: $path"
}

$relativeData = Join-Path $PWD.Path 'fake-data\PowerToys'
foreach ($path in '.\fake-data\PowerToys', '.\fake-data\PowerToys\out', '.\fake-data\PowerToys\..\PowerToys\out')
{
    $rejected = $false
    try { $null = Resolve-OutputDirectory -Directory $path -DataFolder $relativeData -TakeOver $true }
    catch { $rejected = $_.Exception.Message -like 'OutputDirectory must be outside*' }
    Assert $rejected "Reject relative protected output: $path"
}

foreach ($path in 'C:\external-output', 'C:\fake-data\PowerToys-results')
{
    Assert ((Resolve-OutputDirectory -Directory $path -DataFolder $dataFolder -TakeOver $true) -eq $path) "Accept external or prefix-sibling output: $path"
}
Assert ((Resolve-OutputDirectory -Directory $dataFolder -DataFolder $dataFolder -TakeOver $false) -eq $dataFolder) 'Non-takeover output remains allowed'
Assert ((Resolve-OutputDirectory -Directory '.\fake-output\..\results' -DataFolder $dataFolder -TakeOver $true) -eq (Join-Path $PWD.Path 'results')) 'Relative output follows PowerShell location and normalizes parent segments'
Push-Location $PSScriptRoot
try
{
    Assert ((Resolve-OutputDirectory -Directory '.\results' -DataFolder $dataFolder -TakeOver $true) -eq (Join-Path $PSScriptRoot 'results')) 'Relative output respects a changed PowerShell location'
}
finally
{
    Pop-Location
}

$rootStart = @($ast.EndBlock.Statements | Where-Object { $_.Extent.Text -eq '$rootProvider = $null' })[0].Extent.StartOffset
$rootEnd = @($ast.EndBlock.Statements | Where-Object { $_.Extent.Text -like '$runnerPath = Join-Path*' })[0].Extent.StartOffset
$resolveRoot = [scriptblock]::Create($ast.Extent.Text.Substring($rootStart, $rootEnd - $rootStart) + "`nreturn `$root")
$clrLocation = [Environment]::CurrentDirectory
Push-Location $PSScriptRoot
try
{
    # No directories are created: the CLR location is an existing ancestor, unlike the PS location.
    [Environment]::CurrentDirectory = Split-Path $PSScriptRoot -Parent
    $PowerToysRoot = '.\x64\Release\..\Release'
    Assert ((& $resolveRoot) -eq (Join-Path $PSScriptRoot 'x64\Release')) 'Build root follows PS location rather than divergent CLR cwd'
    $PowerToysRoot = 'C:\fake-build\..\build'
    Assert ((& $resolveRoot) -eq 'C:\build') 'Absolute build root normalizes parent segments'
    $PowerToysRoot = 'FileSystem::C:\fake-build'
    Assert ((& $resolveRoot) -eq 'C:\fake-build') 'Filesystem-provider-qualified build root accepted'
    $PowerToysRoot = 'Env:\PATH'
    $rejected = $false
    try { $null = & $resolveRoot } catch { $rejected = $_.Exception.Message -eq 'PowerToysRoot must be a filesystem path.' }
    Assert $rejected 'Non-filesystem build root rejected'
}
finally
{
    [Environment]::CurrentDirectory = $clrLocation
    Pop-Location
}

function New-TestProcess
{
    param([int]$Id = 21, [string]$Fault = 'none', [string]$Path = 'C:\fake-build\module.exe')
    $process = [pscustomobject]@{
        Id = $Id; SessionId = 2; ProcessName = 'PowerToys'; Path = $Path; Handle = [IntPtr]::Zero
        HasExited = ($Fault -eq 'exited'); Fault = $Fault; Kills = 0; Waits = 0; Disposals = 0
        StartTime = [datetime]::UtcNow; StartInfo = [pscustomobject]@{ FileName = $Path }
    }
    $process | Add-Member -MemberType ScriptMethod -Name Kill -Value {
        $this.Kills++
        if ($this.Fault -eq 'kill') { throw 'Injected kill failure' }
        if ($this.Fault -eq 'race') { $this.HasExited = $true; throw 'Exited during kill' }
        if ($this.Fault -notin 'timeout', 'false-success', 'wait') { $this.HasExited = $true }
    }
    $process | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value {
        param($TimeoutMs)
        $this.Waits++
        if ($this.Fault -eq 'wait') { throw 'Injected wait failure' }
        if ($this.Fault -eq 'false-success') { return $true }
        return $this.HasExited
    }
    $process | Add-Member -MemberType ScriptMethod -Name Dispose -Value { $this.Disposals++ }
    $process | Add-Member -MemberType ScriptMethod -Name CloseMainWindow -Value { return $false }
    return $process
}
function Get-TestDescendants
{
    if ($script:descendantDiscoveryFailure) { throw 'Injected descendant discovery failure' }
    return $script:descendants
}
function Get-TestTrayWindow { return [IntPtr]::Zero }
function Send-TestTrayClose {}
$script:descendants = @()

foreach ($fault in 'none', 'exited', 'race', 'kill', 'wait', 'timeout', 'false-success')
{
    $process = New-TestProcess -Fault $fault
    $threw = $false
    $message = ''
    try { $output = @(Stop-Processes -Processes @($process) -TimeoutMs 0) } catch { $threw = $true; $message = $_.Exception.Message }
    Assert ($threw -eq ($fault -in 'kill', 'wait', 'timeout', 'false-success')) "Actual Stop-Processes exit verdict: $fault"
    if ($threw)
    {
        Assert ($message -like '*pid 21*' -and $message -like '*manually*') "Actual stop failure is actionable: $fault"
        if ($fault -eq 'kill') { Assert ($message -like '*Injected kill failure*') 'Original kill exception remains in actionable stopping diagnostics' }
    }
    else
    {
        Assert ($output.Count -eq 0) "Successful stop emits no success-shaped output: $fault"
    }
    Assert ($process.Kills -eq [int]($fault -ne 'exited')) "Only live fake process receives Kill: $fault"
}
$bad = New-TestProcess -Id 22 -Fault kill
$good = New-TestProcess -Id 23
$threw = $false
try { Stop-Processes -Processes @($bad, $good) -TimeoutMs 0 } catch { $threw = $true }
Assert ($threw -and $good.HasExited -and $good.Waits -eq 1) 'Stop-Processes attempts all processes before propagating failure'
Assert (@(Stop-Processes -Processes @() -TimeoutMs 0).Count -eq 0) 'Empty stop succeeds silently'

foreach ($fault in 'none', 'exited', 'kill', 'wait', 'timeout')
{
    $process = New-TestProcess -Fault $fault
    $threw = $false
    try { Wait-PinnedProcesses -Processes @($process) -TimeoutMs 0 } catch { $threw = $true }
    Assert ($threw -eq ($fault -in 'kill', 'wait', 'timeout')) "Pinned child final wait propagates failure: $fault"
}
$parent = New-TestProcess -Fault kill
$child = New-TestProcess -Id 24
$script:descendants = @($child)
$threw = $false
try { Stop-ProcessAndDescendants -Process $parent } catch { $threw = $true }
Assert ($threw -and $child.Disposals -eq 1) 'Descendant handles disposed even when parent stop fails'
$parent = New-TestProcess
$child = New-TestProcess -Id 24 -Fault timeout
$script:descendants = @($child)
$threw = $false
try { Stop-ProcessAndDescendants -Process $parent } catch { $threw = $true }
Assert ($threw -and $parent.HasExited -and $child.Disposals -eq 1) 'Descendant stop failure propagates through actual parent stopping'
$script:descendants = @()

$sessionId = 2
$root = 'C:\fake-build'
$script:handleReads = New-Object 'System.Collections.Generic.List[int]'
$script:processes = @(
    [pscustomobject]@{ Id = 11; SessionId = 2; ProcessName = 'PowerToys'; Path = 'C:\fake-build\PowerToys.exe' },
    [pscustomobject]@{ Id = 12; SessionId = 6; ProcessName = 'PowerToys'; Path = 'C:\fake-build\PowerToys.exe' },
    [pscustomobject]@{ Id = 13; SessionId = 2; ProcessName = 'PowerToys.Settings'; Path = 'C:\fake-build-other\PowerToys.Settings.exe' },
    [pscustomobject]@{ Id = 14; SessionId = 2; ProcessName = 'OtherApp'; Path = 'C:\fake-build\OtherApp.exe' }
)
foreach ($process in $script:processes)
{
    $process | Add-Member -MemberType ScriptProperty -Name Handle -Value {
        $script:handleReads.Add($this.Id)
        return [IntPtr]::Zero
    }
}
function Get-Process
{
    param([string]$Name, [int]$Id, $ErrorAction)
    return $script:processes | Where-Object { $_.HasExited -ne $true -and (($Name -and $_.ProcessName -like $Name) -or ($Id -and $_.Id -eq $Id)) }
}
$selected = @(Get-RootProcesses)
Assert ($selected.Count -eq 1 -and $selected[0].Id -eq 11) 'Discovery preserves current-session executable-directory and name filters'
Assert (-not $script:handleReads.Contains(12)) 'Foreign session excluded before handle access'
Assert ($script:handleReads.Contains(11) -and $script:handleReads.Contains(13)) 'Current-session handles pinned before path comparison'
Assert (-not $script:handleReads.Contains(14)) 'Process name filter preserved'
Assert (@(Get-RootProcesses -Name 'PowerToys.Settings' -Folder 'C:\fake-build-other').Count -eq 1) 'Explicit discovery name and folder preserved'

$runnerPath = 'C:\fake-build\PowerToys.exe'
foreach ($fault in 'none', 'kill', 'wait', 'timeout', 'false-success')
{
    $process = New-TestProcess -Fault $fault
    $script:processes = @($process)
    $threw = $false
    try { Stop-RootProcesses } catch { $threw = $true }
    Assert ($threw -eq ($fault -ne 'none') -and $process.Disposals -eq 1) "Root stop propagates failures and disposes handles: $fault"

    $runner = New-TestProcess -Fault $fault -Path $runnerPath
    $script:processes = @()
    $threw = $false
    try { Stop-Runner -Process $runner -Folder $root } catch { $threw = $true }
    Assert ($threw -eq ($fault -ne 'none')) "Runner stopping validates final exit: $fault"

    $settings = New-TestProcess -Fault $fault
    $threw = $false
    try { Close-Settings -Process $settings } catch { $threw = $true }
    Assert ($threw -eq ($fault -ne 'none')) "Settings stopping validates final exit: $fault"
}
$runner = New-TestProcess -Path $runnerPath
$runner.ProcessName = 'PowerToys'
$script:processes = @($runner)
$script:stoppedRunnerPaths = New-Object System.Collections.Generic.List[string]
Stop-AllRunners
Assert ($runner.HasExited -and $runner.Disposals -eq 1 -and $script:stoppedRunnerPaths.Contains($runnerPath)) 'Takeover stops, records, and disposes original runner'
$runner = New-TestProcess -Fault kill -Path $runnerPath
$unprocessedRunner = New-TestProcess -Id 29 -Path $runnerPath
$script:processes = @($runner, $unprocessedRunner)
$script:stoppedRunnerPaths.Clear()
$threw = $false
try { Stop-AllRunners } catch { $threw = $true }
Assert ($threw -and $runner.Disposals -eq 1 -and $script:stoppedRunnerPaths.Count -eq 0) 'Failed takeover propagates, disposes, and does not record failed stop'
Assert ($unprocessedRunner.Disposals -eq 1 -and $unprocessedRunner.Kills -eq 0) 'Failed initial takeover disposes unprocessed enumerated runner objects'
$script:processes = @()

$powerToysDataFolder = $dataFolder
$backupPath = 'C:\external-output\data-backup'
$resultPath = 'C:\external-output\run.json'
function Reset-Recovery
{
    $script:files = @{
        "$dataFolder\settings.json" = 'changed'
        "$dataFolder\new.json" = 'generated'
        "$dataFolder\Logs\current.log" = 'log'
        "$backupPath\settings.json" = 'original'
        $resultPath = 'result'
    }
    $script:folders = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($folder in $dataFolder, "$dataFolder\Logs", "$dataFolder\empty", $backupPath)
    {
        $null = $script:folders.Add($folder)
    }
    $script:dataSnapshot = [pscustomobject]@{
        Path = $backupPath
        Files = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        Folders = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    }
    $null = $script:dataSnapshot.Files.Add('settings.json')
    $script:copyFailure = $false
    $script:deleteFailure = $null
    $script:enumerationFailure = $false
    $script:launched = @()
    $script:processes = @()
    $script:descendants = @()
    $script:descendantDiscoveryFailure = $false
    $script:unconfirmedProcessExit = $false
    $script:backups = @{}
    $script:exportedBackups = @{}
    $script:exportFailure = $false
    $script:OutputDirectory = 'C:\external-output'
    $script:started = [datetime]'2026-10-09T12:00:00'
    $script:warnings = New-Object 'System.Collections.Generic.List[string]'
    $script:restarts = New-Object 'System.Collections.Generic.List[string]'
    $script:disposed = 0
    $script:stopped = 0
    $script:restoredFiles = 0
    $script:tookOver = $true
    $script:stoppedRunnerPaths = @('C:\fake-runner.exe')
    $script:recorder = New-Object psobject
    $script:recorder | Add-Member -MemberType ScriptMethod -Name Dispose -Value { $script:disposed++ }
}

# Every state-changing command used by the extracted code is replaced; no native types are loaded.
function Test-Path
{
    param([string]$LiteralPath)
    return $script:files.ContainsKey($LiteralPath) -or $script:folders.Contains($LiteralPath) -or $LiteralPath -eq 'C:\fake-runner.exe'
}
function Get-ChildItem
{
    param([string]$LiteralPath, [switch]$Recurse, [switch]$File, [switch]$Directory, [switch]$Force)
    if ($script:enumerationFailure) { throw 'Injected enumeration failure' }
    $prefix = $LiteralPath.TrimEnd('\') + '\'
    $paths = if ($Directory) { @($script:folders) } elseif ($File) { @($script:files.Keys) } else { @($script:files.Keys) + @($script:folders) }
    foreach ($path in $paths)
    {
        if ($path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and ($Recurse -or -not $path.Substring($prefix.Length).Contains('\')))
        {
            [pscustomobject]@{ FullName = $path; PSIsContainer = $script:folders.Contains($path) }
        }
    }
}
function Get-FileHash
{
    param([string]$LiteralPath)
    if (-not $script:files.ContainsKey($LiteralPath)) { throw "Missing source: $LiteralPath" }
    return [pscustomobject]@{ Hash = $script:files[$LiteralPath] }
}
function New-Item
{
    param([string]$ItemType, [switch]$Force, [string]$Path)
    $null = $script:folders.Add($Path)
}
function Copy-Item
{
    param([string]$LiteralPath, [string]$Destination, [switch]$Force)
    if ($script:copyFailure) { throw 'Injected copy failure' }
    if (-not $script:files.ContainsKey($LiteralPath)) { throw "Missing source: $LiteralPath" }
    $script:files[$Destination] = $script:files[$LiteralPath]
}
function Remove-Item
{
    param([string]$LiteralPath, [switch]$Force, [switch]$Recurse, $ErrorAction)
    if ($LiteralPath -eq $script:deleteFailure) { throw 'Injected delete failure' }
    $script:files.Remove($LiteralPath)
    $null = $script:folders.Remove($LiteralPath)
    if ($Recurse)
    {
        foreach ($path in @($script:files.Keys))
        {
            if ($path.StartsWith($LiteralPath + '\', [StringComparison]::OrdinalIgnoreCase)) { $script:files.Remove($path) }
        }
    }
}
function Write-Warning
{
    param([string]$Message)
    $script:warnings.Add($Message)
}
function Restore-Files { $script:restoredFiles++ }
function Export-Clixml
{
    param([Parameter(ValueFromPipeline)]$InputObject, [string]$LiteralPath)
    process
    {
        if ($script:exportFailure) { throw 'Injected backup export failure' }
        $script:exportedBackups[$LiteralPath] = $InputObject.Clone()
    }
}
function Start-Process
{
    param([string]$FilePath)
    $script:restarts.Add($FilePath)
}

Reset-Recovery
$status = @(Restore-DataFolder)
Assert ($status.Count -eq 1 -and $status[0] -is [bool] -and $status[0]) 'Successful restore returns exactly one true Boolean'
Assert ($script:files["$dataFolder\settings.json"] -eq 'original') 'Original settings restored'
Assert (-not $script:files.ContainsKey("$dataFolder\new.json") -and -not $script:folders.Contains("$dataFolder\empty")) 'Generated file and empty directory removed'
Assert ($script:files["$dataFolder\Logs\current.log"] -eq 'log') 'Logs preserved'
Assert ($script:files[$resultPath] -eq 'result') 'Result JSON survives successful restore'
Assert (-not $script:files.ContainsKey("$backupPath\settings.json")) 'Successful recovery removes backup'

Reset-Recovery
$script:files["$dataFolder\settings.json"] = 'original'
$script:copyFailure = $true
$status = @(Restore-DataFolder)
Assert ($status.Count -eq 1 -and $status[0] -is [bool] -and $status[0]) 'Unchanged original skips copying and still restores successfully'

foreach ($fault in 'copy', 'delete', 'directory')
{
    Reset-Recovery
    switch ($fault)
    {
        'copy' { $script:copyFailure = $true }
        'delete' { $script:deleteFailure = "$dataFolder\new.json" }
        'directory' { $script:deleteFailure = "$dataFolder\empty" }
    }
    $status = @(Restore-DataFolder)
    Assert ($status.Count -eq 1 -and $status[0] -is [bool] -and -not $status[0]) "$fault failure returns exactly one false Boolean"
    Assert ($script:files["$backupPath\settings.json"] -eq 'original') "$fault failure retains recovery original"
    Assert (($script:warnings -join ' ') -like "*$backupPath*") "$fault failure reports recovery location"
}

foreach ($fault in 'none', 'copy', 'delete', 'directory', 'unexpected', 'no-snapshot', 'stop')
{
    Reset-Recovery
    switch ($fault)
    {
        'copy' { $script:copyFailure = $true }
        'delete' { $script:deleteFailure = "$dataFolder\new.json" }
        'directory' { $script:deleteFailure = "$dataFolder\empty" }
        'unexpected' { $script:enumerationFailure = $true }
        'no-snapshot' { $script:dataSnapshot = $null; $script:tookOver = $false }
        'stop' { $script:launched = @(New-TestProcess -Fault kill) }
    }
    $threw = $false
    try { & $finalizer } catch { $threw = $true }
    Assert ($threw -eq ($fault -eq 'stop')) "Finalizer preserves unexpected cleanup error visibility: $fault"
    Assert ($script:disposed -eq 1) "Recorder disposed independently: $fault"
    if ($fault -in 'none', 'no-snapshot')
    {
        Assert ($script:restarts.Count -eq 1 -and $script:restarts[0] -eq 'C:\fake-runner.exe') "Safe restart preserved: $fault"
    }
    else
    {
        Assert ($script:restarts.Count -eq 0) "No restart after failure: $fault"
        Assert ($script:files["$backupPath\settings.json"] -eq 'original') "Finalizer retains backup: $fault"
        if ($fault -ne 'stop')
        {
            Assert (($script:warnings -join ' ') -like '*restart was skipped*' -and ($script:warnings -join ' ') -like "*$backupPath*") "Actionable skipped-restart warning: $fault"
        }
        else
        {
            Assert ($script:restoredFiles -eq 0 -and $script:launched[0].Disposals -eq 1) 'Actual failed launched stop withholds all restoration and disposes process'
            Assert (($script:warnings -join ' ') -like "*$backupPath*" -and ($script:warnings -join ' ') -like '*Stop the remaining processes*') 'Actual stopping failure reports recovery originals and required manual stop'
        }
    }
}

foreach ($fault in 'none', 'exited', 'race', 'kill', 'wait', 'timeout', 'false-success', 'root', 'child', 'runner')
{
    Reset-Recovery
    $process = New-TestProcess -Fault $fault
    if ($fault -eq 'root')
    {
        $process = New-TestProcess -Fault timeout
        $script:processes = @($process)
    }
    else
    {
        if ($fault -eq 'runner') { $process = New-TestProcess -Fault kill -Path $runnerPath }
        $script:launched = @($process)
    }
    $child = New-TestProcess -Id 25 -Fault timeout
    $script:descendants = if ($fault -eq 'child') { @($child) } else { @() }
    $threw = $false
    try { & $finalizer } catch { $threw = $true }
    $failed = $fault -in 'kill', 'wait', 'timeout', 'false-success', 'root', 'child', 'runner'
    Assert ($threw -eq $failed) "Actual stopping/finalizer failure propagation: $fault"
    Assert ($script:disposed -eq 1 -and $process.Disposals -eq 1) "Actual stopping/finalizer disposes recorder and process: $fault"
    if ($failed)
    {
        Assert ($script:restoredFiles -eq 0 -and $script:restarts.Count -eq 0 -and $script:files["$dataFolder\settings.json"] -eq 'changed') "Failed actual stopping withholds restore and restart: $fault"
        Assert ($script:files["$backupPath\settings.json"] -eq 'original' -and ($script:warnings -join ' ') -like "*$backupPath*") "Failed actual stopping retains and identifies recovery original: $fault"
    }
    else
    {
        Assert ($script:restoredFiles -eq 1 -and $script:restarts.Count -eq 1 -and $script:files["$dataFolder\settings.json"] -eq 'original') "Successful actual stopping restores and restarts: $fault"
    }
}
$script:descendants = @()
Reset-Recovery
$bad = New-TestProcess -Fault kill
$good = New-TestProcess -Id 26
$script:launched = @($bad, $good)
$rootProcess = New-TestProcess -Id 27
$script:processes = @($rootProcess)
$threw = $false
try { & $finalizer } catch { $threw = $true }
Assert ($threw -and $good.HasExited -and $rootProcess.HasExited) 'Finalizer attempts remaining launched and root stopping after a failed launched stop'
Assert ($bad.Disposals -eq 1 -and $good.Disposals -eq 1 -and $rootProcess.Disposals -eq 1 -and $script:disposed -eq 1) 'Aggregated stopping failure disposes every owned handle and recorder'

Reset-Recovery
$parent = New-TestProcess
$child = New-TestProcess -Id 28 -Fault timeout
$script:descendants = @($child)
$script:launched = @($parent)
$threw = $false
try { Stop-ProcessAndDescendants -Process $parent } catch { $threw = $true }
Assert ($threw -and $parent.HasExited -and $child.Disposals -eq 1) 'Sample cleanup exposes failed child wait and releases pinned handle'
$script:descendants = @()
$threw = $false
try { & $finalizer } catch { $threw = $true }
Assert ($threw -and $script:restoredFiles -eq 0 -and $script:restarts.Count -eq 0) 'Earlier child failure cannot disappear when finalizer sees exited parent'
Assert ($script:disposed -eq 1 -and $parent.Disposals -eq 1 -and $script:files["$backupPath\settings.json"] -eq 'original') 'Earlier child failure retains snapshot and disposes finalizer resources'

Reset-Recovery
$script:dataSnapshot = $null
$script:tookOver = $false
$runner = New-TestProcess -Fault kill -Path $runnerPath
$script:processes = @($runner)
$threw = $false
try { Stop-AllRunners } catch { $threw = $true }
Assert ($threw -and $script:unconfirmedProcessExit) 'Failed initial takeover records unconfirmed exit before takeover flag'
$threw = $false
try { & $finalizer } catch { $threw = $true }
Assert ($threw -and $script:restarts.Count -eq 0 -and $script:restoredFiles -eq 0 -and $script:disposed -eq 1) 'Partial takeover failure cannot restart already-stopped runners'

Reset-Recovery
$process = New-TestProcess
$script:launched = @($process)
$script:descendantDiscoveryFailure = $true
$threw = $false
try { & $finalizer } catch { $threw = $true }
Assert ($threw -and $script:restoredFiles -eq 0 -and $script:restarts.Count -eq 0 -and $process.Disposals -eq 1) 'Unknown descendants fail closed through actual stopping path'

foreach ($exportFault in $false, $true)
{
    Reset-Recovery
    $script:dataSnapshot = $null
    $script:tookOver = $false
    $script:launched = @(New-TestProcess -Fault timeout)
    $script:backups = @{ 'C:\fake-data\last-run.log' = [byte[]](1, 2, 3); 'C:\fake-data\absent.log' = $null }
    $script:exportFailure = $exportFault
    $threw = $false
    try { & $finalizer } catch { $threw = $true }
    Assert ($threw -and $script:restoredFiles -eq 0 -and $script:restarts.Count -eq 0 -and $script:disposed -eq 1) "Individual-file recovery withheld under writer failure: export failure $exportFault"
    if ($exportFault)
    {
        Assert (($script:warnings -join ' ') -like "*Couldn't save individual-file originals*" -and $script:backups.Count -eq 2) 'Recovery export failure surfaces limited in-memory originals instead of success'
    }
    else
    {
        $saved = $script:exportedBackups['C:\external-output\file-backup-20261009-120000.clixml']
        Assert ($saved.Count -eq 2 -and ($saved['C:\fake-data\last-run.log'] -join ',') -eq '1,2,3' -and $null -eq $saved['C:\fake-data\absent.log']) 'Stopping failure saves individual-file bytes and original-absence metadata'
        Assert (($script:warnings -join ' ') -like '*file-backup-20261009-120000.clixml*') 'Stopping failure identifies individual-file recovery location'
    }
}

Write-Host "PASS: $script:assertions assertions; PowerShell $($PSVersionTable.PSVersion); memory-only evidence (no benchmark execution)."
