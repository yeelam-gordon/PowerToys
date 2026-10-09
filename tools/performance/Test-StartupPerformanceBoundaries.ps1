<#
.SYNOPSIS
Tests startup benchmark boundaries with memory-only process and filesystem mocks.
.DESCRIPTION
Extracts actual functions and the finalizer without executing the measurement script. Compiles
its C# and probes descendant discovery with fake Win32 return values, never real native access.
Requires no test framework; supports Windows PowerShell 5.1 and PowerShell 7.
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
    'Stop-LaunchedProcesses', 'Stop-Runner', 'Stop-AllRunners', 'Close-Settings',
    'Restore-Files', 'Save-FileBackups', 'Measure-SvgThumbnail')
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
    $text = $text.Replace('System.Diagnostics.Process]', 'object]')
    $text = $text.Replace('[PowerToysPerformance.ProcessTree]::GetDescendants($Process.Id, $Process.StartTime.ToUniversalTime())', '(Get-TestDescendants)')
    $text = $text.Replace('[IO.File]::WriteAllBytes($path, $script:backups[$path])', '(Write-TestBytes -Path $path -Bytes $script:backups[$path])')
    $text = $text.Replace("[PowerToysPerformance.WindowFinder]::FindTopLevelWindow(`$Process.Id, 'PToyTrayIconWindow')", '(Get-TestTrayWindow)')
    $text = $text.Replace('[PowerToysPerformance.WindowFinder]::PostClose($trayWindow)', '(Send-TestTrayClose)')
    . ([scriptblock]::Create($text))
}
$restoreFilesBody = ${function:Restore-Files}

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

& {
    # Compile all production declarations without invoking them. The second assembly keeps the
    # actual PinnedProcess/ProcessTree bodies; only native entry points and last-error reads are faked.
    $nativeLiteral = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.StringConstantExpressionAst] -and
            $node.Value.StartsWith("using System;") -and $node.Value.Contains('public static class ProcessTree')
    }, $true)
    $nativeSource = $nativeLiteral.Value
    Add-Type -TypeDefinition ($nativeSource.Replace('namespace PowerToysPerformance', 'namespace ProductionCompileOnly'))
    Assert ($null -ne ('ProductionCompileOnly.ProcessTree' -as [type])) 'Complete production C# compiles (no native invocation)'
    foreach ($method in 'CreateToolhelp32Snapshot', 'Process32First', 'Process32Next', 'OpenProcess', 'GetProcessTimes')
    {
        $declaration = [regex]::Match($nativeSource, '(?s)\[DllImport\([^\]]+\)\]\s+public static extern [^\r\n]+ ' + $method + '\(').Value
        Assert ($declaration.Contains('SetLastError = true')) "Production $method preserves Win32 last-error details"
    }
    $bodyStart = $nativeSource.IndexOf('public sealed class PinnedProcess')
    $bodyEnd = $nativeSource.IndexOf('public static class WindowFinder')
    $nativeBody = $nativeSource.Substring($bodyStart, $bodyEnd - $bodyStart).Replace('Marshal.GetLastWin32Error()', 'NativeMethods.GetLastError()')
    $fakeNative = @'
    public static class NativeMethods
    {
        public const uint TH32CS_SNAPPROCESS = 2, PROCESS_QUERY_LIMITED_INFORMATION = 4096,
            SYNCHRONIZE = 1048576, PROCESS_TERMINATE = 1, WAIT_OBJECT_0 = 0;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        public struct PROCESSENTRY32 { public uint dwSize, th32ProcessID, th32ParentProcessID; }
        public struct PROCESS_BASIC_INFORMATION { public IntPtr InheritedFromUniqueProcessId; }
        public static int[] Ids, Parents, ActualParents;
        public static long[] Created;
        public static List<int> Opened = new List<int>(), Closed = new List<int>();
        public static string Fault;
        public static int FaultId, Error, Index;
        public static void Reset(string fault, int faultId, long rootTime)
        {
            Fault = fault; FaultId = faultId; Error = 0; Index = 0;
            Ids = new int[] { 101, 102, 103, 999 };
            Parents = new int[] { 100, 100, 101, 4 };
            ActualParents = (int[])Parents.Clone();
            Created = new long[] { rootTime + 1, rootTime + 2, rootTime + 3, rootTime };
            Opened.Clear(); Closed.Clear();
        }
        public static int GetLastError() { return Error; }
        public static IntPtr CreateToolhelp32Snapshot(uint flags, uint pid)
        {
            Error = 5;
            return Fault == "snapshot" ? INVALID_HANDLE_VALUE : new IntPtr(500);
        }
        public static bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry)
        {
            if (Fault == "first") { Error = 5; return false; }
            if (Fault == "empty") { Error = 18; return false; }
            Index = -1;
            return Process32Next(snapshot, ref entry);
        }
        public static bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry)
        {
            Index++;
            if (Fault == "next" && Index == 1) { Error = 5; return false; }
            if (Index >= Ids.Length) { Error = 18; return false; }
            entry.th32ProcessID = (uint)Ids[Index];
            entry.th32ParentProcessID = (uint)Parents[Index];
            return true;
        }
        public static IntPtr OpenProcess(uint access, bool inherit, uint pid)
        {
            Opened.Add((int)pid);
            if ((int)pid == FaultId && (Fault == "open" || Fault == "gone"))
            {
                Error = Fault == "gone" ? 87 : 5;
                return IntPtr.Zero;
            }
            return new IntPtr(pid);
        }
        public static bool GetProcessTimes(IntPtr handle, out long created, out long exit, out long kernel, out long user)
        {
            created = Created[Array.IndexOf(Ids, handle.ToInt32())]; exit = kernel = user = 0;
            if (handle.ToInt32() == FaultId && Fault == "times") { Error = 5; return false; }
            return true;
        }
        public static int NtQueryInformationProcess(IntPtr handle, int kind, ref PROCESS_BASIC_INFORMATION info, int size, out int length)
        {
            length = size;
            if (handle.ToInt32() == FaultId && Fault == "query") { return unchecked((int)0xC0000022); }
            info.InheritedFromUniqueProcessId = new IntPtr(ActualParents[Array.IndexOf(Ids, handle.ToInt32())]);
            return 0;
        }
        public static bool CloseHandle(IntPtr handle) { Closed.Add(handle.ToInt32()); return true; }
        public static uint WaitForSingleObject(IntPtr handle, uint timeout) { return WAIT_OBJECT_0; }
        public static bool TerminateProcess(IntPtr handle, uint code) { throw new Exception("No fake child should need termination"); }
    }
'@
    Add-Type -TypeDefinition ("using System; using System.Collections.Generic; using System.Runtime.InteropServices; namespace NativeBoundaryProbe {`n" + $nativeBody + $fakeNative + "`n}")
    $script:probeTime = [datetime]'2026-10-09T00:00:00Z'
    foreach ($fault in 'none', 'snapshot', 'first', 'empty', 'next', 'open', 'times', 'query', 'gone', 'parent-mismatch', 'creation-mismatch', 'gone-parent')
    {
        $faultId = if ($fault -eq 'gone-parent') { 101 } else { 102 }
        $nativeFault = if ($fault -eq 'gone-parent') { 'gone' } else { $fault }
        [NativeBoundaryProbe.NativeMethods]::Reset($nativeFault, $faultId, $probeTime.ToFileTimeUtc())
        if ($fault -eq 'parent-mismatch') { [NativeBoundaryProbe.NativeMethods]::ActualParents[1] = 4 }
        if ($fault -eq 'creation-mismatch') { [NativeBoundaryProbe.NativeMethods]::Created[1] = $probeTime.ToFileTimeUtc() - 1 }
        $threw = $false
        $message = ''
        $pinned = @()
        try { $pinned = @([NativeBoundaryProbe.ProcessTree]::GetDescendants(100, $probeTime)) }
        catch { $threw = $true; $message = $_.Exception.Message }
        $failed = $fault -in 'snapshot', 'first', 'next', 'open', 'times', 'query', 'gone-parent'
        Assert ($threw -eq $failed) "Compiled production discovery handles native return representation: $fault"
        Assert (-not [NativeBoundaryProbe.NativeMethods]::Opened.Contains(999)) "Unrelated protected/system PID never inspected: $fault"
        if ($failed)
        {
            $expectedDetail = if ($fault -eq 'query') { '*NTSTATUS 0xC0000022*' } elseif ($fault -eq 'gone-parent') { '*Win32 87*' } else { '*Win32 5*' }
            Assert ($message -like $expectedDetail) "Compiled discovery preserves actionable native error: $fault"
            if ($fault -in 'open', 'times', 'query', 'gone-parent')
            {
                foreach ($id in [NativeBoundaryProbe.NativeMethods]::Opened)
                {
                    if (($fault -in 'open', 'gone-parent') -and $id -eq $faultId) { continue }
                    Assert ([NativeBoundaryProbe.NativeMethods]::Closed.Contains($id)) "Discovery abort releases acquired candidate handle $id`: $fault"
                }
            }
        }
        else
        {
            $expectedCount = if ($fault -eq 'empty') { 0 } elseif ($fault -in 'gone', 'parent-mismatch', 'creation-mismatch') { 2 } else { 3 }
            Assert ($pinned.Count -eq $expectedCount) "Compiled discovery returns only identity-proved children: $fault"
            foreach ($child in $pinned) { $child.Dispose() }
            if ($fault -in 'parent-mismatch', 'creation-mismatch')
            {
                Assert ([NativeBoundaryProbe.NativeMethods]::Closed.Contains(102)) "Rejected identity handle is disposed: $fault"
            }
        }
        Assert ([NativeBoundaryProbe.NativeMethods]::Closed.Contains(500) -eq ($fault -ne 'snapshot')) "Snapshot handle disposal: $fault"
    }
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
    $process | Add-Member -MemberType ScriptMethod -Name get_Handle -Value { return $this.Handle }
    $process | Add-Member -MemberType ScriptMethod -Name get_MainModule -Value { return [pscustomobject]@{ FileName = $this.Path } }
    $process | Add-Member -MemberType ScriptMethod -Name get_HasExited -Value { return $this.HasExited }
    $process | Add-Member -MemberType ScriptMethod -Name get_SessionId -Value { return $this.SessionId }
    return $process
}
function Get-TestDescendants
{
    if ($script:compiledDiscovery)
    {
        return [NativeBoundaryProbe.ProcessTree]::GetDescendants(100, $probeTime)
    }
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
    $process | Add-Member -MemberType ScriptMethod -Name Dispose -Value {}
    $process | Add-Member -MemberType ScriptMethod -Name get_Handle -Value { return $this.Handle }
    $process | Add-Member -MemberType ScriptMethod -Name get_MainModule -Value { return [pscustomobject]@{ FileName = $this.Path } }
    $process | Add-Member -MemberType ScriptMethod -Name get_HasExited -Value { return $false }
    $process | Add-Member -MemberType ScriptMethod -Name get_SessionId -Value { return $this.SessionId }
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
foreach ($accessor in 'get_SessionId', 'get_Handle', 'get_MainModule', 'get_HasExited')
{
    Assert ($null -ne [Diagnostics.Process].GetMethod($accessor)) "Production explicit accessor exists without accessing a real process: $accessor"
}

foreach ($fault in 'access', 'path', 'session', 'exited-access', 'exited-session', 'foreign-access')
{
    $good = New-TestProcess -Id 31
    $candidate = New-TestProcess -Id 32
    if ($fault -in 'exited-access', 'exited-session') { $candidate.HasExited = $true }
    if ($fault -eq 'foreign-access') { $candidate.SessionId = 6 }
    $candidate | Add-Member -MemberType ScriptMethod -Name get_Handle -Force -Value {
        if ($this.Fault -ne 'path') { throw [ComponentModel.Win32Exception]::new(5, 'Injected access denial') }
        return [IntPtr]::Zero
    }
    $candidate.Fault = $fault
    if ($fault -eq 'path')
    {
        $candidate | Add-Member -MemberType ScriptMethod -Name get_MainModule -Force -Value { throw [ComponentModel.Win32Exception]::new(5, 'Injected path denial') }
    }
    if ($fault -in 'session', 'exited-session')
    {
        $candidate | Add-Member -MemberType ScriptMethod -Name get_SessionId -Force -Value { throw [ComponentModel.Win32Exception]::new(5, 'Injected session denial') }
    }
    # Keep an exited candidate in the snapshot to exercise the enumeration-to-access race.
    $script:processes = @($good, $candidate)
    function Get-Process { param($Name, $Id, $ErrorAction) return $script:processes }
    $script:unconfirmedProcessExit = $false
    $threw = $false
    $selected = @()
    try { $selected = @(Get-RootProcesses) } catch { $threw = $true; $message = $_.Exception.Message }
    $failed = $fault -in 'access', 'path', 'session'
    Assert ($threw -eq $failed -and $script:unconfirmedProcessExit -eq $failed) "Actual root discovery distinguishes access failure from positive exit/session exclusion: $fault"
    if ($failed)
    {
        Assert ($message -like '*pid 32*Win32 5*manually*') "Root candidate discovery includes identity and Win32 details: $fault"
        Assert ($good.Disposals -eq 1 -and $candidate.Disposals -eq 1) "Root discovery abort disposes selected and unresolved handles: $fault"
        Assert ($good.Kills -eq 0 -and $candidate.Kills -eq 0) "Root discovery failure never uses PID-only termination: $fault"
    }
    else
    {
        Assert ($selected.Count -eq 1 -and $selected[0].Id -eq 31 -and $candidate.Disposals -eq 1) "Positive exit/foreign session safely excluded and disposed: $fault"
        $good.Dispose()
    }
}
function Get-Process
{
    param([string]$Name, [int]$Id, $ErrorAction)
    return $script:processes | Where-Object { $_.HasExited -ne $true -and (($Name -and $_.ProcessName -like $Name) -or ($Id -and $_.Id -eq $Id)) }
}

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
    $script:compiledDiscovery = $false
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
    $script:byteWriteFailure = $false
    $script:tookOver = $true
    $script:stoppedRunnerPaths = @('C:\fake-runner.exe')
    $script:recorder = New-Object psobject
    $script:recorder | Add-Member -MemberType ScriptMethod -Name Dispose -Value { $script:disposed++ }
}

# Every state-changing command/native entry point used by the extracted code is replaced.
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
function Write-TestBytes
{
    param([string]$Path, [byte[]]$Bytes)
    if ($script:byteWriteFailure) { throw 'Injected byte-write failure' }
    $script:files[$Path] = $Bytes
}
function Restore-Files { $script:restoredFiles++; & $restoreFilesBody }
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

foreach ($fault in 'none', 'exited', 'race', 'kill', 'wait', 'timeout', 'false-success', 'root', 'child', 'runner', 'exited-child', 'exited-child-success')
{
    Reset-Recovery
    $process = New-TestProcess -Fault $fault
    if ($fault -in 'exited-child', 'exited-child-success') { $process = New-TestProcess -Fault exited }
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
    if ($fault -eq 'exited-child-success') { $child.Fault = 'none' }
    $script:descendants = if ($fault -in 'child', 'exited-child', 'exited-child-success') { @($child) } else { @() }
    $threw = $false
    try { & $finalizer } catch { $threw = $true }
    $failed = $fault -in 'kill', 'wait', 'timeout', 'false-success', 'root', 'child', 'runner', 'exited-child'
    Assert ($threw -eq $failed) "Actual stopping/finalizer failure propagation: $fault"
    Assert ($script:disposed -eq 1 -and $process.Disposals -eq 1) "Actual stopping/finalizer disposes recorder and process: $fault"
    if ($fault -in 'exited-child', 'exited-child-success')
    {
        Assert ($process.Kills -eq 0 -and $child.Kills -eq 1 -and $child.Disposals -eq 1) 'Exited parent does not get killed but surviving owned child is stopped and disposed'
    }
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

foreach ($fault in 'snapshot', 'first', 'next', 'open', 'times', 'query')
{
    Reset-Recovery
    $process = New-TestProcess
    $script:launched = @($process)
    $script:compiledDiscovery = $true
    [NativeBoundaryProbe.NativeMethods]::Reset($fault, 102, $probeTime.ToFileTimeUtc())
    $threw = $false
    try { & $finalizer } catch { $threw = $true }
    Assert ($threw -and $script:unconfirmedProcessExit -and $script:restoredFiles -eq 0 -and $script:restarts.Count -eq 0) "Native failure returns reach actual production catch/finalizer: $fault"
    Assert ($script:disposed -eq 1 -and $process.Disposals -eq 1 -and $process.Kills -eq 0) "Native discovery abort disposes resources without unproved kill: $fault"
}

Reset-Recovery
$candidate = New-TestProcess -Id 41
$candidate | Add-Member -MemberType ScriptMethod -Name get_Handle -Force -Value { throw [ComponentModel.Win32Exception]::new(5, 'Injected candidate access denial') }
$script:processes = @($candidate)
$threw = $false
try { & $finalizer } catch { $threw = $true }
Assert ($threw -and $script:unconfirmedProcessExit -and $script:restoredFiles -eq 0 -and $script:restarts.Count -eq 0) 'Actual inaccessible same-session root candidate cannot become absent during final recovery'
Assert ($candidate.Kills -eq 0 -and $candidate.Disposals -eq 1 -and $script:disposed -eq 1 -and $script:files["$backupPath\settings.json"] -eq 'original') 'Root candidate access failure preserves originals and disposes resources without unproved termination'

foreach ($fault in 'bytes', 'absence', 'bytes-export')
{
    Reset-Recovery
    $script:tookOver = $false
    $script:dataSnapshot = $null
    $file = "$dataFolder\last-run.log"
    $absent = "$dataFolder\absent.log"
    $script:backups = @{ $file = [byte[]](1, 2, 3); $absent = $null }
    $script:files[$file] = [byte[]](9)
    $script:files[$absent] = [byte[]](8)
    $script:byteWriteFailure = $fault -in 'bytes', 'bytes-export'
    $script:deleteFailure = if ($fault -eq 'absence') { $absent } else { $null }
    $script:exportFailure = $fault -eq 'bytes-export'
    $status = @(Restore-Files)
    Assert ($status.Count -eq 1 -and $status[0] -is [bool] -and -not $status[0]) "Actual individual restore reports failure as a single Boolean: $fault"
    & $finalizer
    Assert ($script:restarts.Count -eq 0 -and $script:disposed -eq 1 -and $script:backups.Count -eq 2) "Failed individual recovery withholds restart and keeps memory originals: $fault"
    if ($script:exportFailure)
    {
        Assert ($script:exportedBackups.Count -eq 0 -and ($script:warnings -join ' ') -like '*only until this script exits*') 'Individual restore plus export failure honestly reports nondurable cache'
    }
    else
    {
        $saved = $script:exportedBackups['C:\external-output\file-backup-20261009-120000.clixml']
        Assert ($saved.Count -eq 2 -and ($saved[$file] -join ',') -eq '1,2,3' -and $null -eq $saved[$absent]) "Failed individual restore retains bytes and original absence in recovery export: $fault"
        Assert (($script:warnings -join ' ') -like '*file-backup-20261009-120000.clixml*') "Failed individual restore reports durable recovery location: $fault"
    }
}
Reset-Recovery
$script:backups = @{ "$dataFolder\last-run.log" = [byte[]](1, 2, 3); "$dataFolder\missing.log" = $null }
$status = @(Restore-Files)
Assert ($status.Count -eq 1 -and $status[0] -is [bool] -and $status[0]) 'Actual individual restore succeeds for bytes and already-absent original'
Assert (($script:files["$dataFolder\last-run.log"] -join ',') -eq '1,2,3' -and $script:backups.Count -eq 2) 'Successful individual restore writes originals without destroying recovery cache'

# Exercise the production thumbnail body with no launches, file I/O or native access.
function Start-TargetProcess { param($Path, $Arguments) $script:launched = @($script:thumbnailProcess); return $script:thumbnailProcess }
function Add-Sample { param($Name, $Iteration, $Metrics) $script:thumbnailSamples++ }
function Get-ElapsedMs { param($From, $To) return 0 }
$workFolder = 'C:\fake-work'
$svgThumbnailPath = 'C:\fake-build\PowerToys.SvgThumbnailProvider.exe'
$WarmupIterations = 0
$Iterations = 1
foreach ($childFault in 'none', 'timeout')
{
    Reset-Recovery
    $script:tookOver = $false
    $script:files["$workFolder\sample.svg"] = 'sample'
    $script:thumbnailSamples = 0
    $script:thumbnailProcess = New-TestProcess -Fault exited
    $child = New-TestProcess -Id 40 -Fault $childFault
    $script:descendants = @($child)
    $threw = $false
    $message = ''
    try { Measure-SvgThumbnail } catch { $threw = $true; $message = $_.Exception.Message }
    Assert ($threw -and $script:thumbnailSamples -eq 0 -and $script:thumbnailProcess.Kills -eq 0 -and $child.Disposals -eq 1 -and $child.Kills -eq 1) "Missing thumbnail output still attempts owned-child cleanup, not exited parent termination: $childFault"
    if ($childFault -eq 'none')
    {
        Assert ($message -like '*without writing*' -and $child.HasExited) 'Missing-output diagnostic follows successful family cleanup'
    }
    else
    {
        $script:descendants = @()
        $threw = $false
        try { & $finalizer } catch { $threw = $true }
        Assert ($threw -and $script:unconfirmedProcessExit -and $script:restarts.Count -eq 0 -and $script:restoredFiles -eq 0 -and $script:disposed -eq 1) 'Missing-thumbnail child cleanup failure gates all recovery and disposes recorder'
    }
}

Write-Host "PASS: $script:assertions assertions; PowerShell $($PSVersionTable.PSVersion); memory-only evidence (no benchmark execution)."
