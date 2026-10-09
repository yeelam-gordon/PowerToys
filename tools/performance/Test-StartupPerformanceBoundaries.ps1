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

foreach ($name in 'Get-RootProcesses', 'Resolve-OutputDirectory', 'Test-LogFile', 'Restore-DataFolder')
{
    $definition = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    if ($null -eq $definition)
    {
        throw "Missing production function: $name"
    }

    . ([scriptblock]::Create($definition.Extent.Text))
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
    param([string]$Name, $ErrorAction)
    return $script:processes | Where-Object { $_.ProcessName -like $Name }
}
$selected = @(Get-RootProcesses)
Assert ($selected.Count -eq 1 -and $selected[0].Id -eq 11) 'Discovery preserves current-session executable-directory and name filters'
Assert (-not $script:handleReads.Contains(12)) 'Foreign session excluded before handle access'
Assert ($script:handleReads.Contains(11) -and $script:handleReads.Contains(13)) 'Current-session handles pinned before path comparison'
Assert (-not $script:handleReads.Contains(14)) 'Process name filter preserved'
Assert (@(Get-RootProcesses -Name 'PowerToys.Settings' -Folder 'C:\fake-build-other').Count -eq 1) 'Explicit discovery name and folder preserved'

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
    $script:stopFailure = $false
    $script:warnings = New-Object 'System.Collections.Generic.List[string]'
    $script:restarts = New-Object 'System.Collections.Generic.List[string]'
    $script:disposed = 0
    $script:stopped = 0
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
function Stop-LaunchedProcesses
{
    if ($script:stopFailure) { throw 'Injected stop failure' }
}
function Stop-RootProcesses { $script:stopped++ }
function Restore-Files {}
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
        'stop' { $script:stopFailure = $true }
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
    }
}

Write-Host "PASS: $script:assertions assertions; PowerShell $($PSVersionTable.PSVersion); memory-only evidence (no benchmark execution)."
