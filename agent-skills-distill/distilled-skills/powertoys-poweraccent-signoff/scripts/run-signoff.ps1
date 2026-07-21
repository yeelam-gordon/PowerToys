<#
.SYNOPSIS
    End-to-end PowerAccent (Quick Accent) sign-off: build test projects + glyph
    driver, then run the behavioral + lifecycle harness and emit reports.

.DESCRIPTION
    Orchestrates the three REAL executors used by run_signoff.py:
      1. (optional) rebuild the module's MSTest projects (Common/Core) so vstest
         runs against fresh binaries.
      2. build the reflection GlyphDriver.exe (dotnet build) over the built
         PowerAccent.Common.dll.
      3. invoke run_signoff.py -> results.json + report_generated.md, gated on P0.

    The end-user overlay-summon path is NOT covered (blocked under RDP by
    synthetic-input denial). Run this on the interactive console session to also
    exercise the blocked overlay checks. See SKILL.md "Coverage & Limits".

.PARAMETER Python
    Python 3.9+ executable (default: 'python').

.PARAMETER Release
    PowerToys x64\Release root (default: C:\s\powertoys\x64\Release).

.PARAMETER RebuildTests
    Also rebuild the Common/Core MSTest projects before running (VsDevCmd + msbuild).

.PARAMETER Skip
    Comma list of executor kinds to skip and pass through: vstest,glyph,lifecycle.

.EXAMPLE
    ./run-signoff.ps1
.EXAMPLE
    ./run-signoff.ps1 -RebuildTests -Python "C:\Python312\python.exe"
#>
[CmdletBinding()]
param(
    [string]$Python = "python",
    [string]$Release = "C:\s\powertoys\x64\Release",
    [switch]$RebuildTests,
    [string]$Skip = ""
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

function Find-VsDevCmd {
    $c = @(
        "C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\Tools\VsDevCmd.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $c) { throw "VsDevCmd.bat not found. Install VS with C++/.NET build tools." }
    return $c
}

# 1. optional: rebuild the module's MSTest projects
if ($RebuildTests) {
    $vsdev = Find-VsDevCmd
    foreach ($p in @('Common','Core')) {
        $csproj = "C:\s\powertoys\src\modules\poweraccent\PowerAccent.$p.UnitTests\PowerAccent.$p.UnitTests.csproj"
        Write-Host "[build] MSTest $p -> $csproj"
        & $env:ComSpec /c "call `"$vsdev`" -arch=amd64 -host_arch=amd64 >nul && msbuild `"$csproj`" /p:Configuration=Release /p:Platform=x64 /t:Build /v:m /m /nologo" 2>&1 | Select-Object -Last 3
        if ($LASTEXITCODE -ne 0) { throw "MSTest $p build failed (exit $LASTEXITCODE)" }
    }
}

# 2. build the reflection glyph driver (skip if glyph is being skipped)
$glyphExe = $null
if ($Skip -notmatch 'glyph') {
    $csproj = Join-Path $here "glyphdriver\glyphdriver.csproj"
    Write-Host "[build] GlyphDriver -> $csproj"
    & dotnet build $csproj -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "GlyphDriver build failed (exit $LASTEXITCODE)" }
    $glyphExe = Get-ChildItem -Path (Join-Path $here "glyphdriver\bin\Release") -Recurse -Filter GlyphDriver.exe |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $glyphExe) { throw "GlyphDriver.exe not found after build" }
}

# 3. run the harness
$py = @((Join-Path $here "run_signoff.py"), "--release", $Release)
if ($glyphExe) { $py += @("--glyph-exe", $glyphExe) }
if ($Skip)     { $py += @("--skip", $Skip) }
Write-Host "[run] $Python $($py -join ' ')"
& $Python @py
$code = $LASTEXITCODE
Write-Host "[done] harness exit=$code (0=GATE PASS, 1=GATE FAIL, 2=setup error)"
exit $code
