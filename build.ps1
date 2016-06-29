# The cmdlet binding makes common parameters available. The common parameters are
# -Verbose
# -Debug
# -WarningAction
# -WarningVariable
# -ErrorAction
# -ErrorVariable
# -OutVariable
# -OutBuffer
[CmdletBinding()]
param (
    [ValidateSet("debug", "release")]
    [Alias('config')]
    [string]$Configuration = 'debug',
    [ValidateSet("Release", `
                 "alpha", `
                 "beta1", "beta2", "beta3", "beta4", "beta5", `
                 "rc1", "rc2", "rc3", "rc4", "rc5", `
                 "local")]
    [Alias('label')]
    [string]$PrereleaseTag = 'local',
    [ValidateRange(1,99999)]
    [Alias('build')]
    [int]$BuildNumber,
    [string]$CommitId = "0000000000000000000000000000000000000000",
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [switch]$SkipTests
)

trap
{
    Write-Host "Build failed: $_" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\build\common.ps1"

# TODO: Use Psake
# Clean-Projects
#return

# Write prologue
Write-Host ("`r`n" * 3)
Say ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Say "Build #$BuildNumber started at $startTime"

Push-Location $RepoRoot

$BuildErrors = @()
Invoke-BuildStep 'Cleaning artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet -Prerelease } `
    -ev +BuildErrors

# Invoke-BuildStep 'Installing dotnet CLI' { Install-DotnetCLI } `
#     -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { Restore-SolutionPackages } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Building packages' {
        param($Configuration, $BuildLabel, $BuildNumber, $SkipRestore, $Fast)
        # This will build and package all production code under ./src folder
        BuildPackages $Configuration $BuildLabel $BuildNumber -SkipRestore:$SkipRestore
    } `
    -args $Configuration, $PrereleaseTag, $BuildNumber, $SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Running tests' {
        param($SkipRestore)
        # This will build and execute all tests under the ./test folder
        RunTests -SkipRestore:$SkipRestore
    } `
    -args $SkipRestore `
    -skip:$SkipTests `
    -ev +BuildErrors

Pop-Location

# Write epilogue
Say ('-' * 60)

# TODO: Use stopwatch
$endTime = [DateTime]::UtcNow
Say "Build #$BuildNumber ended at $endTime"
Say "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

if ($BuildErrors) {
    Say "Build's completed with following errors:"
    $BuildErrors | Out-Default
}

Say ('=' * 60)

if ($BuildErrors) {
    Throw $BuildErrors.Count
}

Write-Host ("`r`n" * 3)
