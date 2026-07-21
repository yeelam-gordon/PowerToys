param(
  [Parameter(Mandatory=$true)][string]$Tag
)
$ErrorActionPreference = 'Stop'
$base = 'C:\s\Demo\SkillForDistill\benchmark\results\signoff-powerrename'
$py = 'C:\Users\yeelam\AppData\Local\Programs\Python\Python312\python.exe'
$proj = 'C:\s\powertoys\src\modules\powerrename\PowerRenameUILib\PowerRenameUI.vcxproj'
$vsdev = 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\Tools\VsDevCmd.bat'
$exe = 'C:\s\powertoys\x64\Release\WinUI3Apps\PowerToys.PowerRename.exe'

Write-Host "== Building PowerRenameUI (tag=$Tag) =="
& $env:ComSpec /c "call `"$vsdev`" -arch=amd64 >nul && msbuild `"$proj`" /p:Configuration=Release /p:Platform=x64 -m -clp:ErrorsOnly;Summary"
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED"; exit 3 }
Write-Host ("exe mtime: " + (Get-Item $exe).LastWriteTime)

# kill any running instance
$p = Get-Process PowerToys.PowerRename -ErrorAction SilentlyContinue
if ($p) { $p | ForEach-Object { Stop-Process -Id $_.Id -Force } }
Start-Sleep 2

$files = @("testCase1.txt","testCase2.txt","SpecialCase.txt","report_2020.log") | ForEach-Object { Join-Path "$base\workfiles" $_ }
& $py "$base\run_powerrename_signoff.py" --spec "$base\powerrename.spec.json" --files $files `
    --report-json "$base\regression_$Tag.json" --report-md "$base\regression_$Tag.md" 2>&1 |
    Select-Object -Last 11
