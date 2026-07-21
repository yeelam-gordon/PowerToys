$names = @("PowerToys.AdvancedPaste")
foreach ($n in $names) {
  $procs = Get-Process -Name $n -ErrorAction SilentlyContinue
  foreach ($p in $procs) {
    Write-Host "Killing $n PID $($p.Id)"
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
  }
  if (-not $procs) { Write-Host "No $n processes running" }
}
