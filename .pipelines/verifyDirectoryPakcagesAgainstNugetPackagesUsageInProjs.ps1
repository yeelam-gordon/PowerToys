[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$path
)

Write-Host "Verifying that all NuGet packages referenced in .csproj files are defined in Directory.Packages.props"

# Read Directory.Packages.props to get the list of managed packages
$directoryPackagesPath = "Directory.Packages.props"
if (!(Test-Path $directoryPackagesPath)) {
    Write-Host -ForegroundColor Red "Directory.Packages.props not found"
    exit 1
}

[xml]$directoryPackagesXml = Get-Content $directoryPackagesPath
$managedPackages = @{}

# Extract all PackageVersion elements from Directory.Packages.props
foreach ($item in $directoryPackagesXml.Project.ItemGroup.PackageVersion) {
    if ($item -and $item.Include) {
        $managedPackages[$item.Include] = $item.Version
    }
}

Write-Host "Found $($managedPackages.Count) packages managed in Directory.Packages.props"

# Find all .csproj files
$projFiles = Get-ChildItem $path -Filter *.csproj -Force -Recurse
Write-Host "Scanning $($projFiles.Count) .csproj files for PackageReference elements"

$allReferencedPackages = @{}
$violatingProjects = @()

foreach ($projFile in $projFiles) {
    try {
        [xml]$projXml = Get-Content $projFile.FullName
        $referencedPackages = @()
        
        # Find all PackageReference elements in the project
        foreach ($itemGroup in $projXml.Project.ItemGroup) {
            if ($itemGroup.PackageReference) {
                foreach ($packageRef in $itemGroup.PackageReference) {
                    if ($packageRef.Include) {
                        $packageName = $packageRef.Include
                        $referencedPackages += $packageName
                        $allReferencedPackages[$packageName] = $true
                        
                        # Check if this package is managed in Directory.Packages.props
                        if (!$managedPackages.ContainsKey($packageName)) {
                            $violatingProjects += [PSCustomObject]@{
                                Project = $projFile.FullName
                                Package = $packageName
                                Issue = "Package not found in Directory.Packages.props"
                            }
                        }
                    }
                }
            }
        }
        
        if ($referencedPackages.Count -gt 0) {
            Write-Host "  $($projFile.Name): Found $($referencedPackages.Count) package references"
        }
    }
    catch {
        Write-Warning "Failed to parse $($projFile.FullName): $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "=== VERIFICATION RESULTS ==="
Write-Host "Total unique packages referenced in projects: $($allReferencedPackages.Count)"
Write-Host "Total packages managed in Directory.Packages.props: $($managedPackages.Count)"

if ($violatingProjects.Count -eq 0) {
    Write-Host -ForegroundColor Green "✓ All package references in .csproj files are properly managed in Directory.Packages.props"
    exit 0
}
else {
    Write-Host -ForegroundColor Red "✗ Found $($violatingProjects.Count) package reference violations:"
    Write-Host ""
    
    # Group violations by package for better readability
    $groupedViolations = $violatingProjects | Group-Object Package
    
    foreach ($group in $groupedViolations) {
        Write-Host -ForegroundColor Red "Package '$($group.Name)' not found in Directory.Packages.props"
        Write-Host -ForegroundColor Red "  Referenced in:"
        foreach ($violation in $group.Group) {
            $relativePath = $violation.Project -replace [regex]::Escape((Get-Location).Path + [IO.Path]::DirectorySeparatorChar), ""
            Write-Host -ForegroundColor Red "    $relativePath"
        }
        Write-Host ""
    }
    
    Write-Host -ForegroundColor Yellow "To fix these violations:"
    Write-Host -ForegroundColor Yellow "1. Add missing packages to Directory.Packages.props with appropriate versions"
    Write-Host -ForegroundColor Yellow "2. Remove version specifications from PackageReference elements in .csproj files"
    Write-Host -ForegroundColor Yellow "3. Ensure centralized package management is properly configured"
    
    exit 1
}
