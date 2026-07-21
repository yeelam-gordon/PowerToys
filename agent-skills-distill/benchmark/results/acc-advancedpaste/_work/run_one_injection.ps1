param(
  [string]$WorkDir = "C:\s\Demo\SkillForDistill\benchmark\results\acc-advancedpaste\_work",
  [string]$OutDir  = "C:\s\Demo\SkillForDistill\benchmark\results\acc-advancedpaste"
)
. (Join-Path $WorkDir "injections.ps1")
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
$ctrl = Join-Path $WorkDir "ap_controller.ps1"
$readyFile = Join-Path $WorkDir "controller.ready"
$pidFile = Join-Path $WorkDir "ap.pid"

function Kill-All {
  if (Test-Path $pidFile) { $id = Get-Content $pidFile -ErrorAction SilentlyContinue; if ($id) { Stop-Process -Id ([int]$id) -Force -ErrorAction SilentlyContinue } }
  foreach ($p in @(Get-Process PowerToys.AdvancedPaste -ErrorAction SilentlyContinue)) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
  foreach ($c in @(Get-CimInstance Win32_Process -Filter "Name='pwsh.exe' OR Name='powershell.exe'" | Where-Object { $_.CommandLine -match 'ap_controller' })) { Stop-Process -Id ([int]$c.ProcessId) -Force -ErrorAction SilentlyContinue }
  Start-Sleep 2
}
function Rebuild {
  Push-Location "C:\s\PowerToys"
  $out = & $msbuild "src\modules\AdvancedPaste\AdvancedPaste\AdvancedPaste.csproj" /p:Configuration=Release /p:Platform=x64 "/p:SolutionDir=C:\s\PowerToys\" /m /v:m /nologo 2>&1
  Pop-Location
  return (($out | Select-String "PowerToys.AdvancedPaste.dll") -and -not ($out | Select-String ": error "))
}
function Start-Controller {
  Remove-Item $readyFile -ErrorAction SilentlyContinue
  Start-Process pwsh -ArgumentList @("-NoProfile","-ExecutionPolicy","Bypass","-File",$ctrl) -WindowStyle Hidden | Out-Null
  for ($i=0;$i -lt 40;$i++){ if (Test-Path $readyFile){ Start-Sleep 2; return $true }; Start-Sleep 1 }
  return $false
}

$inj = $Injections | Where-Object { $_.id -eq $env:INJ_ID }
$path = Join-Path $PTRoot $inj.file
$orig = [System.IO.File]::ReadAllText($path)
Kill-All
[System.IO.File]::WriteAllText($path, $orig.Replace($inj.find,$inj.repl))
Write-Host "patched $($inj.id); rebuilding..."
if (-not (Rebuild)) { Write-Host "BUILD FAILED"; git -C $PTRoot checkout -- $inj.file; exit 1 }
Start-Controller | Out-Null
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $WorkDir "run_signoff_clip.ps1") -Basename "inj-$($inj.id)" 2>&1 | Out-Null
$j = Get-Content (Join-Path $OutDir "inj-$($inj.id).json") -Raw | ConvertFrom-Json
$chk = $j.checks | Where-Object { $_.id -eq $inj.check }
Write-Host ("$($inj.id) -> $($inj.check) pass=$($chk.pass) CAUGHT=$(-not $chk.pass)")
Write-Host ("  actual: " + ($chk.actual -replace "`r?`n"," "))
Kill-All
git -C $PTRoot checkout -- $inj.file 2>&1 | Out-Null
Write-Host "reverted; rebuilding clean..."
Rebuild | Out-Null
Kill-All
