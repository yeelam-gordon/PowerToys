$ErrorActionPreference = 'Stop'
Write-Host "PowerShell $($PSVersionTable.PSVersion)"
$installerDirectory = Split-Path $PSScriptRoot -Parent
$generatorPath = Join-Path $installerDirectory 'generateAllFileComponents.ps1'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($generatorPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

$componentGuidNamespace = [guid]'DA7240B1-6FC9-4C3C-B921-50AC82EE65D1'
$platform = 'x64'
foreach ($name in @('New-DeterministicGuid', 'Get-ComponentGuid')) {
    $functionAst = $ast.Find({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $false)
    . ([scriptblock]::Create($functionAst.Extent.Text))
}

$vector = New-DeterministicGuid -namespace ([guid]'6ba7b810-9dad-11d1-80b4-00c04fd430c8') -name 'www.widgets.com'
Assert-True ($vector -eq '21F7F8DE-8051-5B89-8680-0195EF798B6A') 'UUIDv5 reference vector changed'
$inputs = @{ componentId = 'Example_Component'; scope = 'perMachine'; installPath = 'same-target'; fileList = @('a.dll', 'b.dll') }
$original = Get-ComponentGuid @inputs
Assert-True ($original -eq '1C9C7CE1-BB25-543D-BEEE-1E0FADE896C5') 'Existing component identity changed'
$inputs.fileList = @('B.DLL', 'A.DLL')
Assert-True ((Get-ComponentGuid @inputs) -eq $original) 'File order/case affected identity'
$inputs.fileList = @('a.dll', 'b.dll', 'c.dll')
Assert-True ((Get-ComponentGuid @inputs) -eq 'E21A9DEA-315A-5759-8843-4149E3B0B1DF') 'File-set identity changed'
$inputs.fileList = @('a.dll', 'b.dll')
foreach ($field in @('scope', 'installPath')) {
    $saved = $inputs[$field]
    $inputs[$field] = 'different'
    Assert-True ((Get-ComponentGuid @inputs) -ne $original) "$field no longer affects identity"
    $inputs[$field] = $saved
}
$platform = 'ARM64'
Assert-True ((Get-ComponentGuid @inputs) -ne $original) 'Platform no longer affects identity'
Write-Host "PASS UUIDv5 and component identity: $vector; $original"

$statements = @($ast.EndBlock.Statements)
$templates = $statements | Where-Object { $_.Extent.Text.StartsWith('$templateWxsFiles =') }
. ([scriptblock]::Create($templates.Extent.Text))
[xml]$project = [System.IO.File]::ReadAllText((Join-Path $installerDirectory 'PowerToysInstallerVNext.wixproj'))
$compiled = @($project.Project.ItemGroup.Compile | ForEach-Object { $_.Include } |
    Where-Object { $_ -like '$(GeneratedWxsDir)*' } |
    ForEach-Object { $_.Substring('$(GeneratedWxsDir)'.Length) })
Assert-True (@(Compare-Object $templateWxsFiles $compiled).Count -eq 0) 'Generated templates and Compile items differ'
Assert-True (@($project.Project.ItemGroup.None | Where-Object { $_.Include -eq 'Resources.wxs' }).Count -eq 1) 'Resource template is not retained as None'
Assert-True ($compiled.Count -eq 23) 'Unexpected generated-template count'
Assert-True ($ast.Extent.Text.Contains('"*.mp4"')) 'MP4 harvest was lost'
$componentCalls = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Generate-FileComponents'
}, $true))
Assert-True ($componentCalls.Count -eq 70) 'Unexpected harvested component-call count'
foreach ($call in $componentCalls) {
    Assert-True ($call.Extent.Text.Contains('-wxsFilePath $outputDir\')) 'Harvested components bypass generated copies'
}
[xml]$product = [System.IO.File]::ReadAllText((Join-Path $installerDirectory 'Product.wxs'))
$majorUpgrade = $product.SelectSingleNode("//*[local-name()='MajorUpgrade']")
Assert-True (-not $majorUpgrade.HasAttribute('Schedule')) 'Upgrade schedule changed'
Write-Host 'PASS static wiring: 23 copied/compiled templates; 70 generated component calls; MP4 included; default upgrade schedule'

$copyLoop = $statements | Where-Object { $_.Extent.Text.StartsWith('foreach ($templateWxsFile in $templateWxsFiles)') }
$resourceStart = $statements | Where-Object { $_.Extent.Text.StartsWith('$resourcesWxsPath =') }
$resourceEnd = $statements | Where-Object { $_.Extent.Text.StartsWith('Set-Content -LiteralPath $resourcesWxsPath') }
$resourceBody = ($statements | Where-Object {
    $_.Extent.StartOffset -ge $resourceStart.Extent.StartOffset -and $_.Extent.EndOffset -le $resourceEnd.Extent.EndOffset
} | ForEach-Object { $_.Extent.Text }) -join "`r`n"
$resourceTemplatePath = Join-Path $installerDirectory 'Resources.wxs'
$resourceTemplate = [System.IO.File]::ReadAllText($resourceTemplatePath)

# Execute the actual copy loop and satellite block with only in-memory file-system mocks.
foreach ($withSatellites in @($true, $false)) {
    & {
        $outputDir = Join-Path $installerDirectory 'obj\mock\Generated'
        $platform = 'x64'
        $memory = @{}
        $writes = [System.Collections.Generic.List[string]]::new()
        $searches = [System.Collections.Generic.List[string]]::new()
        function Copy-Item($Path, $Destination, [switch]$Force) {
            $memory[$Destination] = [System.IO.File]::ReadAllText($Path)
        }
        function Get-Content($LiteralPath, [switch]$Raw) {
            Assert-True ($memory.ContainsKey($LiteralPath)) "Read outside generated copies: $LiteralPath"
            return $memory[$LiteralPath]
        }
        function Set-Content($LiteralPath, $Value) {
            Assert-True ($memory.ContainsKey($LiteralPath)) "Write outside generated copies: $LiteralPath"
            $memory[$LiteralPath] = $Value
            $writes.Add($LiteralPath)
        }
        function New-Guid { return [guid]::NewGuid() }
        function Get-ChildItem($Path, [switch]$File, $ErrorAction) {
            $searches.Add($Path)
            if (-not $withSatellites) { return }
            $assembly = Split-Path $Path -Leaf
            foreach ($language in @('de-DE', 'de')) {
                [pscustomobject]@{
                    Name = $assembly
                    BaseName = [System.IO.Path]::GetFileNameWithoutExtension($assembly)
                    Directory = [pscustomobject]@{ Name = $language }
                }
            }
        }
        . ([scriptblock]::Create('$PSScriptRoot = $installerDirectory' + "`r`n" + $copyLoop.Extent.Text))
        . ([scriptblock]::Create('$PSScriptRoot = $installerDirectory' + "`r`n" + $resourceBody))
        $generatedPath = Join-Path $outputDir 'Resources.wxs'
        Assert-True ($writes.Count -eq 1 -and $writes[0] -eq $generatedPath) 'Satellite output is not exclusively generated Resources.wxs'
        Assert-True ($searches.Count -eq 2) 'Both satellite assemblies must be discovered'
        $result = $memory[$generatedPath]
        [xml]$parsedResources = $result
        $components = @($parsedResources.SelectNodes("//*[local-name()='Component' and starts-with(@Id, 'LightSwitchCli_')]"))
        if ($withSatellites) {
            Assert-True ($components.Count -eq 4) 'Expected both assemblies in both cultures'
            Assert-True ($result.Contains('<?ifndef env.IsPipeline?>')) 'Pipeline localization directory guard lost'
            Assert-True ($result.Contains('Source="$(var.BinDir)de\System.CommandLine.resources.dll"')) 'Neutral-language payload lost'
            Assert-True ($result.Contains('Source="$(var.BinDir)de-DE\PowerToys.LightSwitch.Cli.resources.dll"')) 'Localized CLI payload lost'
            foreach ($component in $components) {
                Assert-True ($component.RegistryKey.Root -eq '$(var.RegistryScope)') 'Registry scope changed'
                Assert-True ($component.RegistryKey.RegistryValue.KeyPath -eq 'yes') 'Registry key path changed'
                Assert-True ($component.RemoveFolder.On -eq 'uninstall') 'Folder cleanup changed'
                Assert-True ([guid]::Parse($component.Guid) -ne [guid]::Empty) 'Invalid satellite GUID'
            }
        } else {
            Assert-True ($components.Count -eq 0) 'Absent satellites emitted components'
        }
        Assert-True (-not $result.Contains('<!--LightSwitchCliResource')) 'Resource placeholders remain'
        Assert-True ([System.IO.File]::ReadAllText($resourceTemplatePath) -ceq $resourceTemplate) 'Tracked resource template changed'
        Write-Host "PASS memory-only satellite generation: present=$withSatellites; components=$($components.Count); generated-only writes=$($writes.Count)"
    }
}
