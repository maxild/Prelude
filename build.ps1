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
    [string]$Configuration = 'debug',
    [ValidateSet("Release", "alpha", "beta", "build", "local")]
    [string]$BuildLabel = 'local',
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

# Write prologue
Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)
$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

pushd $RepoRoot

$BuildErrors = @()
Invoke-BuildStep 'Cleaning artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning nupkgs' { Clear-Nupkgs } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

# Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
#     -ev +BuildErrors

Invoke-BuildStep 'Installing DNX runtimes' `
    { Install-DNX -v $DnxVersion -u -r CoreCLR -a x64; Install-DNX -v $DnxVersion -u -r CLR -a x64 -Default } `
    -ev +BuildErrors

Invoke-BuildStep 'Building projects' {
        param($Configuration, $BuildLabel, $BuildNumber, $SkipRestore)
        # This will build and package all projects under the ./src folder
        Build-Projects $Configuration $BuildLabel $BuildNumber -SkipRestore:$SkipRestore
    } `
    -args $Configuration, $BuildLabel, $BuildNumber, $SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Running tests' {
        param($SkipRestore)
        # This will build and execute all tests under the ./test folder
        Test-Projects -SkipRestore:$SkipRestore
    } `
    -args $SkipRestore `
    -skip:$SkipTests `
    -ev +BuildErrors

popd

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
