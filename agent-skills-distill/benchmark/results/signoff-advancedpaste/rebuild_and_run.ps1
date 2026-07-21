param([string]$Basename)
$ErrorActionPreference = "Continue"
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
Set-Location "C:\s\PowerToys"
& $msbuild "src\modules\AdvancedPaste\AdvancedPaste.UnitTests\AdvancedPaste.UnitTests.csproj" `
    /p:Configuration=Release /p:Platform=x64 "/p:SolutionDir=C:\s\PowerToys\" /m /v:m /nologo 2>&1 |
    Select-String -Pattern "error|AdvancedPaste ->|AdvancedPaste.UnitTests ->" | Select-Object -Last 6
$py = "C:\Users\yeelam\AppData\Local\Programs\Python\Python312\python.exe"
Set-Location "C:\s\Demo\SkillForDistill\benchmark\results\signoff-advancedpaste"
& $py run_advancedpaste_signoff.py $Basename --no-uia 2>&1 | Select-Object -Last 12
