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
    [switch]$SkipClean
)
$ErrorActionPreference = "Continue"
. (Join-Path $WorkDir "injections.ps1")
. (Join-Path $WorkDir "input_helpers.ps1")
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
$apExe   = "C:\s\PowerToys\x64\Release\WinUI3Apps\PowerToys.AdvancedPaste.exe"
$apDll   = "C:\s\PowerToys\x64\Release\WinUI3Apps\PowerToys.AdvancedPaste.dll"
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
    Push-Location "C:\s\PowerToys"
    $out = & $msbuild "src\modules\AdvancedPaste\AdvancedPaste\AdvancedPaste.csproj" /p:Configuration=Release /p:Platform=x64 "/p:SolutionDir=C:\s\PowerToys\" /m /v:m /nologo 2>&1
    Pop-Location
    $ok = ($out | Select-String -Pattern "PowerToys.AdvancedPaste.dll") -ne $null
    $err = $out | Select-String -Pattern ": error " | Select-Object -First 3
    if (-not $ok -or $err) { Write-Host "BUILD ISSUE:"; $err | ForEach-Object { Write-Host "  $_" } }
    return [bool]$ok -and (-not $err)
}

function Start-Controller {
    Remove-Item $readyFile -ErrorAction SilentlyContinue
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
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $WorkDir "run_signoff_clip.ps1") -Basename $bn 2>&1 | Out-Null
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
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $WorkDir "run_signoff_clip.ps1") -Basename "clean_final" 2>&1 | Out-Null
    Kill-AP
}

$caughtCount = ($records | Where-Object { $_.caught }).Count
$summary = [pscustomobject]@{
    timestamp=(Get-Date).ToString("s"); total=$records.Count; caught=$caughtCount;
    detection_rate="$caughtCount/$($records.Count)"; injections=$records
}
$summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutDir "results_clip.json") -Encoding UTF8
Write-Host "`nDETECTION RATE: $caughtCount/$($records.Count)"
Write-Host "wrote $(Join-Path $OutDir 'results.json')"
