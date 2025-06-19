[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$path
)

Write-Host "Verifying that all NuGet packages referenced in .csproj files are defined in Directory.Packages.props"

# Find all Directory.Packages.props files in the repository
$directoryPackagesFiles = Get-ChildItem $path -Filter "Directory.Packages.props" -Force -Recurse
if ($directoryPackagesFiles.Count -eq 0) {
    Write-Host -ForegroundColor Red "No Directory.Packages.props files found"
    exit 1
}

Write-Host "Found $($directoryPackagesFiles.Count) Directory.Packages.props files:"
foreach ($file in $directoryPackagesFiles) {
    $relativePath = $file.FullName -replace [regex]::Escape((Get-Location).Path + [IO.Path]::DirectorySeparatorChar), ""
    Write-Host "  $relativePath"
}

# Function to find the nearest Directory.Packages.props file for a given project
function Get-NearestDirectoryPackagesProps {
    param([string]$projectPath)
    
    $currentDir = [System.IO.Path]::GetDirectoryName($projectPath)
    
    while ($currentDir -and $currentDir.Length -gt 0) {
        $packagePropsPath = Join-Path $currentDir "Directory.Packages.props"
        if (Test-Path $packagePropsPath) {
            return $packagePropsPath
        }
        
        $parentDir = [System.IO.Path]::GetDirectoryName($currentDir)
        if ($parentDir -eq $currentDir) {
            break  # Reached root directory
        }
        $currentDir = $parentDir
    }
    
    return $null
}

# Function to read packages from a Directory.Packages.props file
function Get-ManagedPackagesFromFile {
    param([string]$filePath)
    
    $managedPackages = @{}
    try {
        [xml]$directoryPackagesXml = Get-Content $filePath
        foreach ($item in $directoryPackagesXml.Project.ItemGroup.PackageVersion) {
            if ($item -and $item.Include) {
                $managedPackages[$item.Include] = $item.Version
            }
        }
    }
    catch {
        Write-Warning "Failed to parse ${filePath}: $($_.Exception.Message)"
    }
    
    return $managedPackages
}

# Find all .csproj files
$projFiles = Get-ChildItem $path -Filter *.csproj -Force -Recurse
Write-Host "Scanning $($projFiles.Count) .csproj files for PackageReference elements"

$allReferencedPackages = @{}
$violatingProjects = @()
$packagePropsCache = @{}

foreach ($projFile in $projFiles) {
    try {
        # Find the nearest Directory.Packages.props file for this project
        $nearestPackageProps = Get-NearestDirectoryPackagesProps $projFile.FullName
        if (-not $nearestPackageProps) {
            Write-Warning "No Directory.Packages.props found for $($projFile.FullName)"
            continue
        }
        
        # Cache the managed packages for this Directory.Packages.props file
        if (-not $packagePropsCache.ContainsKey($nearestPackageProps)) {
            $packagePropsCache[$nearestPackageProps] = Get-ManagedPackagesFromFile $nearestPackageProps
        }
        $managedPackages = $packagePropsCache[$nearestPackageProps]
        
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
                        
                        # Check if this package is managed in the nearest Directory.Packages.props
                        if (!$managedPackages.ContainsKey($packageName)) {
                            $relativePackagePropsPath = $nearestPackageProps -replace [regex]::Escape((Get-Location).Path + [IO.Path]::DirectorySeparatorChar), ""
                            $violatingProjects += [PSCustomObject]@{
                                Project = $projFile.FullName
                                Package = $packageName
                                Issue = "Package not found in Directory.Packages.props"
                                PackagePropsFile = $relativePackagePropsPath
                            }
                        }
                    }
                }
            }
        }
        
        if ($referencedPackages.Count -gt 0) {
            $relativePackagePropsPath = $nearestPackageProps -replace [regex]::Escape((Get-Location).Path + [IO.Path]::DirectorySeparatorChar), ""
            Write-Host "  $($projFile.Name): Found $($referencedPackages.Count) package references (using $relativePackagePropsPath)"
        }
    }
    catch {
        Write-Warning "Failed to parse $($projFile.FullName): $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "=== VERIFICATION RESULTS ==="
Write-Host "Total unique packages referenced in projects: $($allReferencedPackages.Count)"

# Count total managed packages across all Directory.Packages.props files
$totalManagedPackages = 0
foreach ($packageProps in $packagePropsCache.Keys) {
    $relativePackagePropsPath = $packageProps -replace [regex]::Escape((Get-Location).Path + [IO.Path]::DirectorySeparatorChar), ""
    $managedCount = $packagePropsCache[$packageProps].Count
    Write-Host "Packages managed in ${relativePackagePropsPath}: $managedCount"
    $totalManagedPackages += $managedCount
}

if ($violatingProjects.Count -eq 0) {
    Write-Host -ForegroundColor Green "✓ All package references in .csproj files are properly managed in their respective Directory.Packages.props files"
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
            Write-Host -ForegroundColor Red "    $relativePath (should be in $($violation.PackagePropsFile))"
        }
        Write-Host ""
    }
    
    Write-Host -ForegroundColor Yellow "To fix these violations:"
    Write-Host -ForegroundColor Yellow "1. Add missing packages to the appropriate Directory.Packages.props file with appropriate versions"
    Write-Host -ForegroundColor Yellow "2. Remove version specifications from PackageReference elements in .csproj files"
    Write-Host -ForegroundColor Yellow "3. Ensure centralized package management is properly configured"
    
    exit 1
}
