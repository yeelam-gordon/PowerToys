param(
    [Parameter(Mandatory=$true)][string]$Tag,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$py   = 'C:\Users\yeelam\AppData\Local\Programs\Python\Python312\python.exe'
$sk   = 'C:\s\Demo\SkillForDistill\.github\skills\app-signoff-uia\scripts\signoff.py'
$spec = 'C:\s\Demo\SkillForDistill\benchmark\work\envvars.spec.json'
$work = 'C:\s\Demo\SkillForDistill\benchmark\work'
$vsdev= 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\Tools\VsDevCmd.bat'
$proj = 'C:\s\PowerToys\src\modules\EnvironmentVariables\EnvironmentVariablesUILib\EnvironmentVariablesUILib.csproj'
$srcDll = 'C:\s\PowerToys\src\modules\EnvironmentVariables\EnvironmentVariablesUILib\bin\x64\Release\PowerToys.EnvironmentVariablesUILib.dll'
$dstDir = 'C:\s\PowerToys\x64\Release\WinUI3Apps'
$exe    = 'C:\s\PowerToys\x64\Release\WinUI3Apps\PowerToys.EnvironmentVariables.exe'

# 1. kill any running instance (releases DLL lock)
Get-Process PowerToys.EnvironmentVariables -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep -Milliseconds 800

# 2. build UILib (unless skipped)
if (-not $SkipBuild) {
    & $env:ComSpec /c "call `"$vsdev`" -arch=x64 -host_arch=x64 >nul && msbuild `"$proj`" /p:Configuration=Release /p:Platform=x64 -m /v:q /nologo" | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Output "BUILD_FAILED"; exit 3 }
}

# 3. copy fresh DLL into the release folder
Copy-Item $srcDll (Join-Path $dstDir 'PowerToys.EnvironmentVariablesUILib.dll') -Force

# 4. launch app
Start-Process $exe
Start-Sleep -Seconds 6

# 5. find HWND
$line = (winapp ui list-windows 2>&1 | Select-String 'Environment Variables' | Select-Object -First 1).ToString()
$hwnd = [regex]::Match($line, 'HWND (\d+)').Groups[1].Value
if (-not $hwnd) { Write-Output "NO_WINDOW"; exit 4 }

# 6. run signoff
$rjson = Join-Path $work "report_$Tag.json"
$rmd   = Join-Path $work "report_$Tag.md"
& $py $sk run --spec $spec --window $hwnd --report-json $rjson --report-md $rmd 2>$null | Out-Null
$gate = $LASTEXITCODE
$r = Get-Content $rjson -Raw | ConvertFrom-Json
$failed = ($r.checks | Where-Object { $_.status -eq 'FAIL' } | ForEach-Object { $_.id }) -join ','
Write-Output "TAG=$Tag HWND=$hwnd GATE=$($r.gate) PASS=$($r.summary.passed)/$($r.summary.total) FAILED=[$failed]"
