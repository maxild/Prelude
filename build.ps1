#!/usr/bin/env pwsh

<#

.SYNOPSIS
This is a Powershell script to bootstrap a Cake build.

.DESCRIPTION
This Powershell script will ensure cake.tool and gitversion.tool are installed,
and execute your Cake build script with the parameters you provide.

.PARAMETER Target
The task/target to run.
.PARAMETER Configuration
The build configuration to use.
.PARAMETER Verbosity
Specifies the amount of information to be displayed.
.PARAMETER NuGetVersion
The version of nuget.exe to be downloaded.
.PARAMETER ScriptArgs
Remaining arguments are added here.

.LINK
http://cakebuild.net

#>

[CmdletBinding()]
Param(
    [string]$Target = "Default",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Verbose",
    [string]$NuGetVersion = "latest",
    [Parameter(Position = 0, Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string[]]$ScriptArgs
)

$PSScriptRoot = split-path -parent $MyInvocation.MyCommand.Definition
$TOOLS_DIR = Join-Path $PSScriptRoot "tools"

# Make sure tools folder exists
if ((Test-Path $PSScriptRoot) -and (-not (Test-Path $TOOLS_DIR))) {
    Write-Verbose -Message "Creating tools directory..."
    New-Item -Path $TOOLS_DIR -Type directory | out-null
}

###########################################################################
# LOAD versions from build.config
###########################################################################

[string] $DotNetSdkVersion = ''
[string] $CakeVersion = ''
[string] $CakeScriptsVersion = ''
[string] $GitVersionVersion = ''
[string] $GitReleaseManagerVersion = ''
foreach ($line in Get-Content (Join-Path $PSScriptRoot 'build.config')) {
    if ($line -like 'DOTNET_VERSION=*') {
        $DotNetSdkVersion = $line.SubString(15)
    }
    elseif ($line -like 'CAKE_VERSION=*') {
        $CakeVersion = $line.SubString(13)
    }
    elseif ($line -like 'CAKESCRIPTS_VERSION=*') {
        $CakeScriptsVersion = $line.SubString(20)
    }
    elseif ($line -like 'GITVERSION_VERSION=*') {
        $GitVersionVersion = $line.SubString(19)
    }
    elseif ($line -like 'GITRELEASEMANAGER_VERSION=*') {
        $GitReleaseManagerVersion = $line.SubString(26)
    }
}
if ([string]::IsNullOrEmpty($DotNetSdkVersion)) {
    'Failed to parse .NET Core SDK version'
    exit 1
}
if ([string]::IsNullOrEmpty($CakeVersion)) {
    'Failed to parse Cake version'
    exit 1
}
if ([string]::IsNullOrEmpty($CakeScriptsVersion)) {
    'Failed to parse CakeScripts version'
    exit 1
}
if ([string]::IsNullOrEmpty($GitVersionVersion)) {
    'Failed to parse GitVersion version'
    exit 1
}
if ([string]::IsNullOrEmpty($GitReleaseManagerVersion)) {
    'Failed to parse GitReleaseManager version'
    exit 1
}

# This will force the use of TLS 1.2 (you can also make it use 1.1 if you want for some reason).
# To avoid the exception: "The underlying connection was closed: An unexpected error occurred on a send."
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

###########################################################################
# Install .NET Core SDK
###########################################################################

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1 # Caching packages on a temporary build machine is a waste of time.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1       # opt out of telemetry
$env:DOTNET_ROLL_FORWARD = "Major"

$DotNetChannel = 'LTS'

Function Remove-PathVariable([string]$VariableToRemove) {
    $path = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($path -ne $null) {
        $newItems = $path.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { "$($_)" -inotlike $VariableToRemove }
        [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "User")
    }

    $path = [Environment]::GetEnvironmentVariable("PATH", "Process")
    if ($path -ne $null) {
        $newItems = $path.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { "$($_)" -inotlike $VariableToRemove }
        [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "Process")
    }
}

$FoundDotNetSdkVersion = $null
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    # dotnet --version will use version found in global.json, but the SDK will error if the
    # global.json version is not found on the machine.
    $FoundDotNetSdkVersion = & dotnet --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        # Extract the first line of the message without making powershell write any error messages
        Write-Host ($FoundDotNetSdkVersion | ForEach-Object { "$_" } | select-object -first 1)
        Write-Host "That is not problem, we will install the SDK version below."
        $FoundDotNetSdkVersion = "" # Force installation of .NET Core SDK via dotnet-install script
    }
    else {
        Write-Host ".NET Core SDK version $FoundDotNetSdkVersion found."
    }
}

if ($FoundDotNetSdkVersion -ne $DotNetSdkVersion) {
    Write-Verbose -Message "Installing .NET Core SDK version $DotNetSdkVersion ..."

    $InstallPath = Join-Path $PSScriptRoot ".dotnet"
    if (-not (Test-Path $InstallPath)) {
        mkdir -Force $InstallPath | Out-Null
    }

    (New-Object System.Net.WebClient).DownloadFile("https://dot.net/v1/dotnet-install.ps1", "$InstallPath\dotnet-install.ps1")

    & $InstallPath\dotnet-install.ps1 -Channel $DotNetChannel -Version $DotNetSdkVersion -InstallDir $InstallPath -NoPath

    Remove-PathVariable "$InstallPath"
    $env:PATH = "$InstallPath;$env:PATH"
    $env:DOTNET_ROOT = $InstallPath
}

###########################################################################
# Install CakeScripts
###########################################################################

if (-not (Test-Path (Join-Path $TOOLS_DIR 'Maxfire.CakeScripts'))) {
    $NUGET_EXE = Join-Path $TOOLS_DIR 'nuget.exe'
    if ( ($CakeScriptsVersion -eq "latest") -or [string]::IsNullOrWhitespace($CakeScriptsVersion) ) {
        & $NUGET_EXE install Maxfire.CakeScripts -ExcludeVersion -Prerelease -OutputDirectory `"$TOOLS_DIR`" -Source 'https://api.nuget.org/v3/index.json;https://www.myget.org/F/maxfire/api/v3/index.json' | Out-Null
    }
    else {
        & $NUGET_EXE install Maxfire.CakeScripts -Version $CakeScriptsVersion -ExcludeVersion -Prerelease -OutputDirectory `"$TOOLS_DIR`" -Source 'https://api.nuget.org/v3/index.json;https://www.myget.org/F/maxfire/api/v3/index.json' | Out-Null
    }

    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring Maxfire.CakeScripts."
    }
}

###########################################################################
# INSTALL .NET Core 3.x tools
###########################################################################

# To see list of packageid, version and commands
#      dotnet tool list --tool-path ./tools
Function Install-NetCoreTool {
    param
    (
        [string]$PackageId,
        [string]$ToolCommandName,
        [string]$Version
    )

    $ToolPath = Join-Path $TOOLS_DIR '.store' | Join-Path -ChildPath $PackageId.ToLower() | Join-Path -ChildPath $Version
    $ToolPathExists = Test-Path -Path $ToolPath -PathType Container

    $ExePath = (Get-ChildItem -Path $TOOLS_DIR -Filter "${ToolCommandName}*" -File | ForEach-Object FullName | Select-Object -First 1)
    $ExePathExists = (![string]::IsNullOrEmpty($ExePath)) -and (Test-Path $ExePath -PathType Leaf)

    if ((!$ToolPathExists) -or (!$ExePathExists)) {

        if ($ExePathExists) {
            & dotnet tool uninstall --tool-path $TOOLS_DIR $PackageId | Out-Null
        }

        & dotnet tool install --tool-path $TOOLS_DIR --version $Version --configfile NuGet.public.config $PackageId | Out-Null
        if ($LASTEXITCODE -ne 0) {
            "Failed to install $PackageId"
            exit $LASTEXITCODE
        }

        $ExePath = (Get-ChildItem -Path $TOOLS_DIR -Filter "${ToolCommandName}*" -File | ForEach-Object FullName | Select-Object -First 1)
    }

    return $ExePath
}

[string] $CakeExePath = Install-NetCoreTool -PackageId 'Cake.Tool' -ToolCommandName 'dotnet-cake' -Version $CakeVersion
Install-NetCoreTool -PackageId 'GitVersion.Tool' -ToolCommandName 'dotnet-gitversion' -Version $GitVersionVersion | Out-Null
Install-NetCoreTool -PackageId 'GitReleaseManager.Tool' -ToolCommandName 'dotnet-gitreleasemanager' -Version $GitReleaseManagerVersion | Out-Null

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

# When using modules we have to add this
& "$CakeExePath" ./build.cake --bootstrap

# Build the argument list.
$Arguments = @{
    target        = $Target;
    configuration = $Configuration;
    verbosity     = $Verbosity;
}.GetEnumerator() | ForEach-Object { "--{0}=`"{1}`"" -f $_.key, $_.value }

Write-Host "Running build script..."
& "$CakeExePath" ./build.cake $Arguments $ScriptArgs
exit $LASTEXITCODE
