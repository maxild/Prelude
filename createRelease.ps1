# TODO: Move into cake task runner (x-plat)
[CmdletBinding()]
param (
    [switch]$SkipPush
)

$ErrorActionPreference = "Stop"
trap
{
   Pop-Location
   Write-Error "$_"
   Exit 1
}

Push-Location $PSScriptRoot

# Make sure there are no pending changes
$pendingChanges = & git status --porcelain
if ($pendingChanges -ne $null)
{
  throw 'You have pending changes, aborting release'
}

# Pull latest, fast-forward
& git fetch origin
& git checkout master
& git merge origin/master --ff-only

$output = & gitversion /output json
if ($LASTEXITCODE -ne 0) {
    throw "GitVersion Exit Code: $LASTEXITCODE"
}
$versionInfoJson = $output -join "`n"
$versionInfo = $versionInfoJson | ConvertFrom-Json

# This is _not_ a prerelease
$stableVersion = $versionInfo.MajorMinorPatch

# Create a tag
$tagName = "v$stableVersion"
& git tag -a $tagName -m "Create release $tagName"
if ($LASTEXITCODE -ne 0) {
    & git reset --hard HEAD^
    throw "No changes detected since last release"
}

# push the (annotated) tag
if (-not $SkipPush) {
    & git push origin $tagName
}

Pop-Location
