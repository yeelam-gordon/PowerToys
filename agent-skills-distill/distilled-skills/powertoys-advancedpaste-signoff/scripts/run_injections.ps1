<#
Injection acceptance runner for the AdvancedPaste sign-off.

For each of the 10 injections: kills AP, patches one source line, rebuilds the
AdvancedPaste project, relaunches AP (fresh binary) via the ShowUI controller,
runs the full winappcli-driven sign-off, records whether the mapped checklist
item flipped to FAIL (i.e. the bug was caught by driving the REAL app), then
reverts the source. Finally rebuilds clean and re-runs the sign-off.

Requires working synthetic input (the sign-off pastes via SendInput). Verify
input first with verify_input.ps1.
#>
param(
    [string]$OutDir  = "$PSScriptRoot",
    [string]$WorkDir = "$PSScriptRoot",
    [string]$PTRoot  = $env:POWERTOYS_ROOT,
    [string]$MSBuild = $env:POWERTOYS_MSBUILD,
    [switch]$SkipClean
)
$ErrorActionPreference = "Continue"
. (Join-Path $WorkDir "injections.ps1")   # resolves/validates $PTRoot (env-based, fail-fast)
. (Join-Path $WorkDir "input_helpers.ps1")

function Resolve-MSBuild([string]$explicit) {
    if ($explicit -and (Test-Path $explicit)) { return $explicit }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
        if ($found -and (Test-Path $found)) { return $found }
    }
    throw "MSBuild.exe not found. Pass -MSBuild <path>, set `$env:POWERTOYS_MSBUILD, or install Visual Studio with the MSBuild component (auto-resolved via vswhere)."
}

$msbuild = Resolve-MSBuild $MSBuild
$apExe   = Join-Path $PTRoot "x64\Release\WinUI3Apps\PowerToys.AdvancedPaste.exe"
$apDll   = Join-Path $PTRoot "x64\Release\WinUI3Apps\PowerToys.AdvancedPaste.dll"
$ctrl    = Join-Path $WorkDir "ap_controller.ps1"
$pidFile = Join-Path $WorkDir "ap.pid"
$readyFile = Join-Path $WorkDir "controller.ready"

$script:ctrlProc = $null

function Kill-AP {
    if (Test-Path $pidFile) {
        $id = Get-Content $pidFile -ErrorAction SilentlyContinue
        if ($id) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
    }
    Get-Process PowerToys.AdvancedPaste -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
    if ($script:ctrlProc -and -not $script:ctrlProc.HasExited) { Stop-Process -Id $script:ctrlProc.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep 2
}

function Rebuild {
    Push-Location $PTRoot
    try {
        $out = & $msbuild "src\modules\AdvancedPaste\AdvancedPaste\AdvancedPaste.csproj" /p:Configuration=Release /p:Platform=x64 "/p:SolutionDir=$PTRoot\" /m /v:m /nologo 2>&1
    } finally {
        Pop-Location
    }
    $ok = ($out | Select-String -Pattern "PowerToys.AdvancedPaste.dll") -ne $null
    $err = $out | Select-String -Pattern ": error " | Select-Object -First 3
    if (-not $ok -or $err) { Write-Host "BUILD ISSUE:"; $err | ForEach-Object { Write-Host "  $_" } }
    return [bool]$ok -and (-not $err)
}

function Start-Controller {
    Remove-Item $readyFile -ErrorAction SilentlyContinue
    if (-not $env:POWERTOYS_AP_EXE) { $env:POWERTOYS_AP_EXE = $apExe }
    $script:ctrlProc = Start-Process powershell -ArgumentList @("-NoProfile","-ExecutionPolicy","Bypass","-File",$ctrl) -PassThru -WindowStyle Hidden
    for ($i=0; $i -lt 40; $i++) {
        if (Test-Path $readyFile) { Start-Sleep 2; return $true }
        Start-Sleep 1
    }
    return $false
}

function Ensure-Notepad {
    if (-not (Get-NotepadHwnd)) { Start-Process notepad; Start-Sleep 3 }
}

$records = @()

foreach ($inj in $Injections) {
    Write-Host "`n========== $($inj.id) -> $($inj.check) : $($inj.desc) =========="
    $path = Join-Path $PTRoot $inj.file
    $orig = [System.IO.File]::ReadAllText($path)
    if (-not $orig.Contains($inj.find)) { Write-Host "  SKIP: find not present"; continue }

    # Apply injection
    $patched = $orig.Replace($inj.find, $inj.repl)
    Kill-AP
    [System.IO.File]::WriteAllText($path, $patched)
    Write-Host "  patched; rebuilding..."
    $built = Rebuild
    if (-not $built) {
        Write-Host "  BUILD FAILED - reverting"
        git -C $PTRoot checkout -- $inj.file 2>&1 | Out-Null
        $records += [pscustomobject]@{ id=$inj.id; check=$inj.check; desc=$inj.desc; file=$inj.file; caught=$false; note="build failed"; mapped_result="N/A" }
        continue
    }
    Ensure-Notepad
    if (-not (Start-Controller)) { Write-Host "  controller not ready" }

    # Run sign-off against the injected build
    $bn = "inj-$($inj.id)"
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $WorkDir "run_signoff.ps1") -Basename $bn 2>&1 | Out-Null
    $json = Join-Path $OutDir "$bn.json"
    $mappedPass = $null; $mappedActual = ""
    if (Test-Path $json) {
        $r = Get-Content $json -Raw | ConvertFrom-Json
        $chk = $r.checks | Where-Object { $_.id -eq $inj.check }
        if ($chk) { $mappedPass = $chk.pass; $mappedActual = $chk.actual }
    }
    $caught = ($mappedPass -eq $false)
    Write-Host ("  mapped {0} pass={1} => CAUGHT={2}" -f $inj.check, $mappedPass, $caught)

    # Revert
    Kill-AP
    git -C $PTRoot checkout -- $inj.file 2>&1 | Out-Null

    $records += [pscustomobject]@{
        id=$inj.id; check=$inj.check; desc=$inj.desc; file=$inj.file;
        caught=[bool]$caught; mapped_result=$(if($mappedPass -eq $false){"FAIL (caught)"}elseif($mappedPass -eq $true){"PASS (missed)"}else{"NO-RESULT"});
        mapped_actual=$mappedActual; signoff_json="$bn.json"; screenshot_dir="screenshots\$bn"
    }
}

# Restore clean build + optional clean run
Write-Host "`n========== reverting to clean build =========="
Kill-AP
$cleanBuilt = Rebuild
if (-not $SkipClean -and $cleanBuilt) {
    Ensure-Notepad
    Start-Controller | Out-Null
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $WorkDir "run_signoff.ps1") -Basename "clean_final" 2>&1 | Out-Null
    Kill-AP
}

$caughtCount = ($records | Where-Object { $_.caught }).Count
$summary = [pscustomobject]@{
    timestamp=(Get-Date).ToString("s"); total=$records.Count; caught=$caughtCount;
    detection_rate="$caughtCount/$($records.Count)"; injections=$records
}
$summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutDir "results.json") -Encoding UTF8
Write-Host "`nDETECTION RATE: $caughtCount/$($records.Count)"
Write-Host "wrote $(Join-Path $OutDir 'results.json')"
