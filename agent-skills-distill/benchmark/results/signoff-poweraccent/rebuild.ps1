param(
  [Parameter(Mandatory=$true)][ValidateSet('common','core')] [string]$Proj
)
$ErrorActionPreference = 'Stop'
$vsdev = "C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\Tools\VsDevCmd.bat"
if ($Proj -eq 'common') {
  $csproj = "C:\s\powertoys\src\modules\poweraccent\PowerAccent.Common.UnitTests\PowerAccent.Common.UnitTests.csproj"
} else {
  $csproj = "C:\s\powertoys\src\modules\poweraccent\PowerAccent.Core.UnitTests\PowerAccent.Core.UnitTests.csproj"
}
& $env:ComSpec /c "call `"$vsdev`" -arch=amd64 -host_arch=amd64 >nul && msbuild `"$csproj`" /p:Configuration=Release /p:Platform=x64 /t:Build /v:m /m /nologo" 2>&1 | Select-Object -Last 3
Write-Output "REBUILD_EXIT=$LASTEXITCODE"
