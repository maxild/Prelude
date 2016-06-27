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

Clean-Compilation
return

# Write prologue
Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

Push-Location $RepoRoot

$BuildErrors = @()
Invoke-BuildStep 'Cleaning artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Installing dotnet CLI' { Install-DotnetCLI } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { Restore-SolutionPackages } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Building packages' {
        param($Configuration, $BuildLabel, $BuildNumber, $SkipRestore, $Fast)
        Build-Packages $Configuration $BuildLabel $BuildNumber -SkipRestore:$SkipRestore
    } `
    -args $Configuration, $PrereleaseTag, $BuildNumber, $SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Running tests' {
        param($SkipRestore)
        # This will build and execute all tests under the ./test folder
        Run-Tests -SkipRestore:$SkipRestore
    } `
    -args $SkipRestore `
    -skip:$SkipTests `
    -ev +BuildErrors

Pop-Location

# Write epilogue
Trace-Log ('-' * 60)

# TODO: Use stopwatch
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

if ($BuildErrors) {
    Trace-Log "Build's completed with following errors:"
    $BuildErrors | Out-Default
}

Trace-Log ('=' * 60)

if ($BuildErrors) {
    Throw $BuildErrors.Count
}

Write-Host ("`r`n" * 3)
