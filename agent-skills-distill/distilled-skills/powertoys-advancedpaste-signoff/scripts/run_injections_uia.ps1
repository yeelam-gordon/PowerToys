<#
Input-independent injection acceptance runner.

Covers the 3 checklist items that are verified by pure UIA reads (no SendInput /
paste): CHK-07 (AI-box gating), CHK-08 (clipboard preview), CHK-09 (core format
list). For each mapped injection it: kills AP, patches one source line, rebuilds
the AdvancedPaste project, relaunches AP via the ShowUI pipe controller, runs the
REAL winappcli check, screenshots the AP window, records whether the check flipped
to FAIL (bug caught), then reverts the source.

This is the subset that remains provable via real end-to-end winappcli execution
when synthetic input is unavailable (disconnected RDP session). The 7 paste checks
require SendInput and are reported separately.
#>
param(
    [string]$OutDir  = "$PSScriptRoot",
    [string]$WorkDir = "$PSScriptRoot",
    [string]$PTRoot  = $env:POWERTOYS_ROOT,
    [string]$MSBuild = $env:POWERTOYS_MSBUILD
)
$ErrorActionPreference = "Continue"
. (Join-Path $WorkDir "injections.ps1")   # resolves/validates $PTRoot (env-based, fail-fast)

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
$ctrl    = Join-Path $WorkDir "ap_controller.ps1"
$pidFile = Join-Path $WorkDir "ap.pid"
$readyFile = Join-Path $WorkDir "controller.ready"
$trigger = Join-Path $WorkDir "show.trigger"
$ShotDir = Join-Path $OutDir "screenshots\uia-injections"
New-Item -ItemType Directory -Force $ShotDir | Out-Null
$script:ctrlProc = $null

function Kill-AP {
    if (Test-Path $pidFile) { $id = Get-Content $pidFile -ErrorAction SilentlyContinue; if ($id) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue } }
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
    return ([bool]$ok -and (-not $err))
}
function Start-Controller {
    Remove-Item $readyFile -ErrorAction SilentlyContinue
    if (-not $env:POWERTOYS_AP_EXE) { $env:POWERTOYS_AP_EXE = $apExe }
    $script:ctrlProc = Start-Process pwsh -ArgumentList @("-NoProfile","-ExecutionPolicy","Bypass","-File",$ctrl) -PassThru -WindowStyle Hidden
    for ($i=0; $i -lt 40; $i++) { if (Test-Path $readyFile) { Start-Sleep 2; return $true }; Start-Sleep 1 }
    return $false
}
function Show-AP {
    New-Item -ItemType File $trigger -Force | Out-Null; Start-Sleep -Milliseconds 1500
    $m = winapp ui list-windows -a PowerToys.AdvancedPaste 2>&1 | Select-String 'HWND (\d+): "Advanced Paste"'
    for ($i=0; $i -lt 5 -and -not $m; $i++){ Start-Sleep -Milliseconds 700; $m = winapp ui list-windows -a PowerToys.AdvancedPaste 2>&1 | Select-String 'HWND (\d+): "Advanced Paste"' }
    if ($m) { $m.Matches.Groups[1].Value } else { $null }
}
function SetClipText($t){ $tf = Join-Path $WorkDir "_clip_text.tmp"; [System.IO.File]::WriteAllText($tf,$t); powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $WorkDir "set_clipboard.ps1") -Mode text -FromFile $tf | Out-Null }
function Shot($ap,$name){ $p = Join-Path $ShotDir "$name.png"; winapp ui screenshot "Advanced Paste" -w $ap -o $p --focus 2>&1 | Out-Null; $p }

# Each check returns @{ pass=<bool>; actual=<string> } read from the REAL window.
function Check-07($ap){
    $ai = (winapp ui get-property "InputTxtBox" -w $ap --property IsEnabled 2>&1 | Select-String "IsEnabled:\s*(\w+)").Matches.Groups[1].Value
    @{ pass = ($ai -eq "False"); actual = "InputTxtBox IsEnabled=$ai (expect False)" }
}
function Check-08($ap){
    SetClipText "PREVIEW_CHECK_555"; Start-Sleep 1
    $ap = Show-AP
    $found = winapp ui search "PREVIEW_CHECK_555" -w $ap 2>&1 | Select-String "PREVIEW_CHECK_555"
    @{ ap=$ap; pass = [bool]$found; actual = "preview shows PREVIEW_CHECK_555 = $([bool]$found) (expect True)" }
}
function Check-09($ap){
    $hasMd = winapp ui search "Paste as markdown" -w $ap 2>&1 | Select-String "ListItem"
    $hasPlain = winapp ui search "Paste as plain text" -w $ap 2>&1 | Select-String "ListItem"
    $hasJson = winapp ui search "Paste as JSON" -w $ap 2>&1 | Select-String "ListItem"
    @{ pass = ([bool]$hasMd -and [bool]$hasPlain -and [bool]$hasJson); actual = "plain=$([bool]$hasPlain) markdown=$([bool]$hasMd) json=$([bool]$hasJson) (expect all True)" }
}

$targets = "I7","I8","I9"
$records = @()

foreach ($tid in $targets) {
    $inj = $Injections | Where-Object { $_.id -eq $tid }
    Write-Host "`n========== $($inj.id) -> $($inj.check) : $($inj.desc) =========="
    $path = Join-Path $PTRoot $inj.file
    $orig = [System.IO.File]::ReadAllText($path)
    if (-not $orig.Contains($inj.find)) { Write-Host "  SKIP find missing"; continue }
    Kill-AP
    [System.IO.File]::WriteAllText($path, $orig.Replace($inj.find, $inj.repl))
    Write-Host "  patched; rebuilding..."
    if (-not (Rebuild)) { Write-Host "  BUILD FAILED - revert"; [System.IO.File]::WriteAllText($path,$orig); $records += [pscustomobject]@{id=$inj.id;check=$inj.check;desc=$inj.desc;caught=$false;note="build failed"}; continue }
    if (-not (Start-Controller)) { Write-Host "  controller not ready" }
    $ap = Show-AP
    switch ($inj.check) {
        "CHK-07" { $res = Check-07 $ap }
        "CHK-08" { $res = Check-08 $ap; if ($res.ap) { $ap = $res.ap } }
        "CHK-09" { $res = Check-09 $ap }
    }
    $shot = Shot $ap ("inj-$($inj.id)")
    $caught = ($res.pass -eq $false)
    Write-Host ("  {0} pass={1} => CAUGHT={2}" -f $inj.check,$res.pass,$caught)
    Write-Host ("  actual: {0}" -f $res.actual)
    Kill-AP
    git -C $PTRoot checkout -- $inj.file 2>&1 | Out-Null
    $records += [pscustomobject]@{ id=$inj.id; check=$inj.check; desc=$inj.desc; file=$inj.file; caught=[bool]$caught; mapped_result=$(if($res.pass -eq $false){"FAIL (caught)"}else{"PASS (missed)"}); actual=$res.actual; screenshot="screenshots\uia-injections\inj-$($inj.id).png" }
}

Write-Host "`n========== reverting to clean build =========="
Kill-AP
$cleanOk = Rebuild
Write-Host "clean rebuild ok=$cleanOk"

$caughtCount = ($records | Where-Object { $_.caught }).Count
$summary = [pscustomobject]@{ timestamp=(Get-Date).ToString("s"); scope="input-independent UIA checks (CHK-07/08/09)"; total=$records.Count; caught=$caughtCount; detection_rate="$caughtCount/$($records.Count)"; injections=$records }
$summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutDir "results_uia.json") -Encoding UTF8
Write-Host "`nUIA DETECTION: $caughtCount/$($records.Count)"
Write-Host "wrote $(Join-Path $OutDir 'results_uia.json')"
