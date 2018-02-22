##########################################################################
# This is the Cake bootstrapper script for PowerShell.
# This file was downloaded from https://github.com/cake-build/resources
# Feel free to change this file to fit your needs.
##########################################################################

<#
.SYNOPSIS
This is a Powershell script to bootstrap a Cake build.

.DESCRIPTION
This Powershell script will download NuGet if missing, restore NuGet tools (including Cake)
and execute your Cake build script with the parameters you provide.

.PARAMETER Target
The build script target to run.
.PARAMETER Configuration
The build configuration to use.
.PARAMETER Verbosity
Specifies the amount of information to be displayed.
.PARAMETER NuGetVersion
The version of nuget.exe to be downloaded.
.PARAMETER CakeScriptsVersion
The version of Maxfire.CakeScripts to be downloaded.
.PARAMETER ShowVersion
Show version of Cake tool.
.PARAMETER WhatIf
Performs a dry run of the build script.
No tasks will be executed.
.PARAMETER SkipToolPackageRestore
Skips restoring of packages.
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
    [string]$CakeScriptsVersion = "latest",
    [switch]$ShowVersion,
    [Alias("DryRun","Noop")]
    [switch]$WhatIf,
    [switch]$SkipToolPackageRestore,
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$ScriptArgs
)

$PSScriptRoot = split-path -parent $MyInvocation.MyCommand.Definition;

# Tree
$TOOLS_DIR           = Join-Path $PSScriptRoot "tools"
$NUGET_EXE           = Join-Path $TOOLS_DIR "nuget.exe"
$CAKE_EXE            = Join-Path $TOOLS_DIR "Cake/Cake.exe"
$PACKAGES_CONFIG     = Join-Path $TOOLS_DIR "packages.config" # containing Cake dependency
$PACKAGES_CONFIG_MD5 = Join-Path $TOOLS_DIR "packages.config.md5sum"

# Maxfire.CakeScripts version can be pinned
$CakeScriptsVersion = "latest" # 'latest' or 'major.minor.patch'

$DotNetChannel = "Current";
$DotNetVersion = "2.1.4";
$DotNetInstallerUri = "https://dot.net/v1/dotnet-install.ps1";

if ((-not ($NuGetVersion -eq "latest")) -and (-not $NuGetVersion.StartsWith("v"))) {
    $NuGetVersion = ("v" + $NuGetVersion)
}
$NugetUrl = "https://dist.nuget.org/win-x86-commandline/$NuGetVersion/nuget.exe"

# Make sure tools folder exists
if ((Test-Path $PSScriptRoot) -and (-not (Test-Path $TOOLS_DIR))) {
    Write-Verbose -Message "Creating tools directory..."
    New-Item -Path $TOOLS_DIR -Type directory | out-null
}

###########################################################################
# Install .NET Core CLI
###########################################################################

Function Remove-PathVariable([string]$VariableToRemove)
{
    $path = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($path -ne $null)
    {
        $newItems = $path.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { "$($_)" -inotlike $VariableToRemove }
        [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "User")
    }

    $path = [Environment]::GetEnvironmentVariable("PATH", "Process")
    if ($path -ne $null)
    {
        $newItems = $path.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { "$($_)" -inotlike $VariableToRemove }
        [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "Process")
    }
}

# Get .NET Core CLI path if installed.
$FoundDotNetCliVersion = $null;
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $FoundDotNetCliVersion = dotnet --version;
}

# If version mismatch install dotnetcli locally
if ($FoundDotNetCliVersion -ne $DotNetVersion) {
    Write-Verbose -Message "Installing .NET Core SDK Binaries..."
    $InstallPath = Join-Path $PSScriptRoot ".dotnet"
    if (!(Test-Path $InstallPath)) {
        mkdir -Force $InstallPath | Out-Null;
    }
    # The remote server returned an error: (407) Proxy Authentication Required, if behind proxy
    (New-Object System.Net.WebClient).DownloadFile($DotNetInstallerUri, "$InstallPath\dotnet-install.ps1");
    & $InstallPath\dotnet-install.ps1 -Channel $DotNetChannel -Version $DotNetVersion -InstallDir $InstallPath -NoPath;

    Remove-PathVariable "$InstallPath"
    $env:PATH = "$InstallPath;$env:PATH"
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 # Caching packages on a temporary build machine is a waste of time.
    $env:DOTNET_CLI_TELEMETRY_OPTOUT=1       # opt out of telemetry

    & "$InstallPath\dotnet.exe" --info
}

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
$env:DOTNET_CLI_TELEMETRY_OPTOUT=1

###########################################################################
# Install Nuget
###########################################################################

if (-not (Test-Path $NUGET_EXE)) {
    Write-Verbose -Message "Downloading NuGet.exe ($NuGetVersion)..."
    try {
        Invoke-WebRequest $NugetUrl -OutFile $NUGET_EXE
    } catch {
        Throw "Could not download NuGet.exe."
    }
    Write-Verbose -Message (& $NUGET_EXE help | Select-Object -First 1 | Out-String)
}

###########################################################################
# Install Cake and CakeScripts (i.e. tools)
###########################################################################

[Reflection.Assembly]::LoadWithPartialName("System.Security") | Out-Null
function MD5HashFile([string] $filePath)
{
    if ([string]::IsNullOrEmpty($filePath) -or !(Test-Path $filePath -PathType Leaf))
    {
        return $null
    }

    [System.IO.Stream] $file = $null;
    [System.Security.Cryptography.MD5] $md5 = $null;
    try
    {
        $md5 = [System.Security.Cryptography.MD5]::Create()
        $file = [System.IO.File]::OpenRead($filePath)
        return [System.BitConverter]::ToString($md5.ComputeHash($file))
    }
    finally
    {
        if ($file -ne $null)
        {
            $file.Dispose()
        }
    }
}

# Install/restore tools (i.e. Cake) using NuGet
if (-not $SkipToolPackageRestore.IsPresent) {
    Push-Location
    Set-Location $TOOLS_DIR

    # Check for changes in packages.config and remove installed tools if true.
    [string] $md5Hash = MD5HashFile $PACKAGES_CONFIG
    if ( (-not (Test-Path $PACKAGES_CONFIG_MD5)) -Or
      ($md5Hash -ne (Get-Content $PACKAGES_CONFIG_MD5 )) ) {
        Write-Verbose -Message "Missing or changed $PACKAGES_CONFIG_MD5 file..."
        Remove-Item * -Recurse -Exclude packages.config,nuget.exe # remove installed tools (ie. Cake and Maxfire.CakeScripts)
    }

    Write-Verbose -Message "Restoring tools from NuGet..."
    $NuGetOutput = & $NUGET_EXE install $PACKAGES_CONFIG -ExcludeVersion -OutputDirectory `"$TOOLS_DIR`" -Source https://api.nuget.org/v3/index.json
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Throw "An error occured while restoring NuGet tools."
    }
    else
    {
        # save packages.config hash to disk
        $md5Hash | Out-File $PACKAGES_CONFIG_MD5 -Encoding "ASCII"
    }

    Write-Verbose -Message ($NuGetOutput | out-string)
    Pop-Location
}

# Install re-usable cake scripts, using the latest version
# Note: We cannot put the package reference into ./tools/packages.json, because this file does not support floating versions
if (-not $SkipToolPackageRestore.IsPresent) {
    if (-not (Test-Path (Join-Path $TOOLS_DIR 'Maxfire.CakeScripts'))) {
        Write-Verbose -Message "Restoring Maxfire.CakeScripts from MyGet feed..."
        if ( ($CakeScriptsVersion -eq "latest") -or [string]::IsNullOrWhitespace($CakeScriptsVersion) ) {
            $NuGetOutput = & $NUGET_EXE install Maxfire.CakeScripts -ExcludeVersion -Prerelease -OutputDirectory `"$TOOLS_DIR`" -Source 'https://api.nuget.org/v3/index.json;https://www.myget.org/F/maxfire/api/v3/index.json'
        }
        else {
            $NuGetOutput = & $NUGET_EXE install Maxfire.CakeScripts -Version $CakeScriptsVersion -ExcludeVersion -Prerelease -OutputDirectory `"$TOOLS_DIR`" -Source 'https://api.nuget.org/v3/index.json;https://www.myget.org/F/maxfire/api/v3/index.json'
        }
        if ($LASTEXITCODE -ne 0) {
            Throw "An error occured while restoring Maxfire.CakeScripts."
        }
        else
        {
            Write-Verbose -Message ($NuGetOutput | out-string)
        }
    }
}

# Make sure that Cake has been installed.
if (-not (Test-Path $CAKE_EXE)) {
    Throw "Could not find Cake.exe at $CAKE_EXE"
}


###########################################################################
# RUN BUILD SCRIPT
###########################################################################

if ($ShowVersion.IsPresent) {
    & $CAKE_EXE -version
}
else {
    # Build the argument list.
    $Arguments = @{
        target=$Target;
        configuration=$Configuration;
        verbosity=$Verbosity;
        dryrun=$WhatIf;
    }.GetEnumerator() | ForEach-Object {"--{0}=`"{1}`"" -f $_.key, $_.value };

    Write-Host "Running build script..."
    Invoke-Expression "& `"$CAKE_EXE`" `"build.cake`" $Arguments $ScriptArgs"
}
exit $LASTEXITCODE
