#
# Credit goes to Nuget.Client (See https://github.com/NuGet/NuGet.Client/blob/dev/build/common.ps1)
#
. "$PSScriptRoot\filesystem.ps1"

$ValidConfigurations = 'debug', 'release'
$DefaultConfiguration = 'debug'
$ValidBuildLabels = 'Release','rtm', 'rc', 'beta', 'local' # TODO: Not used!!
$DefaultBuildLabel = 'local'

# project tree
$RepoRoot = Get-DirName $PSScriptRoot
$ArtifactsFolder = Join-Path $RepoRoot 'artifacts'
$SrcFolder = Join-Path $RepoRoot 'src'
$TestFolder = Join-Path $RepoRoot 'test'

# restore, build, pack, test
$DotNetCliFolder = Join-Path $RepoRoot '.dotnetcli'
$DotNetExe = Join-Path $DotNetCliFolder 'dotnet.exe'

# Clear-PackageCache, Restore-SolutionPackages
$NuGetFolder = Join-Path $RepoRoot '.nuget'
$NuGetExe = Join-Path $NuGetFolder 'nuget.exe'

# The following aliases will update nuget and dotnet for the powershell session
Set-Alias dotnet $DotNetExe
Set-Alias nuget $NuGetExe

Function Trace-Log($TraceMessage = '') {
    Write-Host "[$(Trace-Time)]`t$TraceMessage" -ForegroundColor Cyan
}

Function Verbose-Log($VerboseMessage) {
    Write-Verbose "[$(Trace-Time)]`t$VerboseMessage"
}

Function Error-Log($ErrorMessage) {
    Write-Error "[$(Trace-Time)]`t$ErrorMessage"
}

Function Warning-Log($WarningMessage) {
    Write-Warning "[$(Trace-Time)]`t$WarningMessage"
}

Function Trace-Time() {
    $currentTime = Get-Date
    $lastTime = $Global:LastTraceTime
    $Global:LastTraceTime = $currentTime
    "{0:HH:mm:ss} +{1:F0}" -f $currentTime, ($currentTime - $lastTime).TotalSeconds
}
$Global:LastTraceTime = Get-Date

Function Format-ElapsedTime($ElapsedTime) {
    '{0:F0}:{1:D2}' -f $ElapsedTime.TotalMinutes, $ElapsedTime.Seconds
}

# Local builds will generate a build number based on the 'duration' since semantic version date
Function Get-BuildNumber() {
    $SemanticVersionDate = '2015-11-30'
    [int](((Get-Date) - (Get-Date $SemanticVersionDate)).TotalMinutes / 5)
}

# D5 means 'pad left with 00000', 1 -> '00001', 2 -> '00002' etc.
Function Format-BuildNumber([int]$BuildNumber) {
    if ($BuildNumber -gt 99999) {
        Throw "Build number cannot be greater than 99999, because of Legacy SemVer limitations in Nuget."
    }
    '{0:D5}' -f $BuildNumber # Can handle 0001,...,99999 (this should be enough)
}

Function Invoke-BuildStep {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True)]
        [string]$BuildStep,
        [Parameter(Mandatory=$True)]
        [ScriptBlock]$Expression,
        [Parameter(Mandatory=$False)]
        [Alias('args')]
        [Object[]]$Arguments,
        [Alias('skip')]
        [switch]$SkipExecution
    )
    if (-not $SkipExecution) {
        Trace-Log "[BEGIN] $BuildStep"
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $completed = $false
        try {
            Invoke-Command $Expression -ArgumentList $Arguments -ErrorVariable err
            $completed = $true
        }
        finally {
            $sw.Stop()
            if ($completed) {
                Trace-Log "[DONE +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
            }
            else {
                if (-not $err) {
                    Trace-Log "[STOPPED +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
                }
                else {
                    Error-Log "[FAILED +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
                }
            }
        }
    }
    else {
        Warning-Log "[SKIP] $BuildStep"
    }
}

Function Test-MSBuildVersionPresent {
    [CmdletBinding()]
    param(
        [string]$MSBuildVersion
    )

    $MSBuildExePath = Get-MSBuildExePath $MSBuildVersion
    Test-Path $MSBuildExePath
}

Function Get-MSBuildExePath {
    param(
        [string]$MSBuildVersion
    )

    Join-Path ${env:ProgramFiles(x86)} 'MSBuild\' | `
    Join-Path -ChildPath ($MSBuildVersion + ".0") | `
    Join-Path -ChildPath 'bin' | `
    Join-Path -ChildPath 'msbuild.exe'
}

Function Install-NuGet {
    [CmdletBinding()]
    param(
        [switch]$Prerelease
    )

    # Create .nuget folder if necessary
    New-Item -ItemType Directory -Force -Path $NuGetFolder | Out-Null

    if ($Prerelease) {
        $NugetVersion = 'latest-prerelease'
    }
    else {
        $NugetVersion = 'latest'
    }

    if (-not (Test-File $NuGetExe)) {
        Trace-Log "Downloading $NugetVersion of nuget.exe."
        Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/$NugetVersion/nuget.exe" -OutFile $NuGetExe
    }
    else {
        Trace-Log "nuget.exe have already been downloaded. Updating to $NugetVersion."
        $opts = 'update', '-Self'
        if ($Prerelease) {
            $opts += '-Prerelease'
        }
        Trace-Log "$NuGetExe $opts"
        & $NuGetExe $opts
    }

    # Display version (nuget --version does not exist)
    & $NuGetExe help | select -first 1 | Out-Default
}

# Note: The official RC2 MSI installer (DotNetCore.1.0.0.RC2-SDK.Preview1-x64.exe) will
#       install version 1.0.0-preview1-002702
Function Install-DotnetCLI {
    [CmdletBinding()]
    param(
        [string] $CLIVersion = "1.0.0-preview2-003030"
    )

    Trace-Log 'Downloading Dotnet CLI'

    # create .dotnetcli subfolder if necessary
    New-Item -ItemType Directory -Force -Path $DotNetCliFolder | Out-Null

    # Windows .NET Core search paths:
    #   1) %DOTNET_HOME%
    #   2) %LOCALAPPDATA%\Microsoft\dotnet (this is where install.ps1 installs by default)
    #   3) %ProgramFiles%\dotnet (Windows MSI installer)
    #
    # Unix???
    #   1) $DOTNET_HOME
    #   2) Maybe a user level search path? ~/.dotnet? While tooling will probably be global-install, I do think we'll want user-installed runtimes
    #   3) /usr/local/share/dotnet
    #   4) /usr/share/dotnet

# CLI notes:
#
# Installing CLI (scripts/obtain/install.ps1)
#   $env:CLI_VERSION="1.0.0-beta-002071"
#   $env:DOTNET_INSTALL_DIR = "$pwd\.dotnetcli"
#   & .\scripts\obtain\install.ps1 -Channel "preview" -version "$env:CLI_VERSION" -InstallDir "$env:DOTNET_INSTALL_DIR" -NoPath

    # Define the DOTNET_HOME directory for tools in the CLI
    $env:DOTNET_HOME=$DotNetCliFolder

    # Define the install root for the script (it defaults to $env:LocalAppData\Microsoft\dotnet)
    $env:DOTNET_INSTALL_DIR=$RepoRoot

    $DotNetInstallScript = Join-Path $DotNetCliFolder "dotnet-install.ps1"

    # wget the ./scripts/obtain/dotnet-install.ps1 script from the 'rel/1.0.0' branch in the github repo
    Invoke-WebRequest 'https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/dotnet-install.ps1' -OutFile $DotNetInstallScript

    # Install a pre-release stable (preview) version
    & $DotNetInstallScript -Channel preview -i $DotNetCliFolder -Version $CLIVersion

    if (-not (Test-Path $DotNetExe)) {
        Error-Log "Unable to find dotnet.exe. The CLI install may have failed." -Fatal
    }

    # Display build info
    #& $DotNetExe --info

    # Display version
    & $DotNetExe --version
}

# Get the sdk version from global.json
Function Get-SdkVersionFromGlobalJson
{
    $jsonData = Get-GlobalJson
    return $jsonData.sdk.version
}

# Get the projects from global.json.
# Note: Build system will search for projects in the
# directories specified here when resolving dependencies.
Function Get-ProjectsFromGlobalJson
{
    $jsonData = Get-GlobalJson
    return $jsonData.projects
}

Function Get-GlobalJson
{
    $globalJson = join-path $RepoRoot "global.json"
    $jsonData = Get-Content -Path $globalJson -Raw | ConvertFrom-JSON
    return $jsonData
}

# dotnet clean is not supported in .NET Core 1.0 (see https://github.com/dotnet/cli/issues/16)
# Clean the directories referenced in the global.json file.
Function Clean-Projects
{
    $projects = Get-ProjectsFromGlobalJson
    $projects | %{Join-Path -Path $RepoRoot -ChildPath $_} | %{
        Trace-Log "Cleaning: $_ recursively"
        Get-ChildItem -Path $_ -Include bin,obj -Recurse | `
        %{ Remove-Item $_.FullName -Recurse -Force }
    }
}

# Remove all content inside ./artifacts folder
Function Clear-Artifacts {
    [CmdletBinding()]
    param()
    if (Test-Dir $ArtifactsFolder) {
        Trace-Log 'Cleaning the Artifacts folder'
        Remove-Item $ArtifactsFolder\* -Recurse -Force
    }
}

Function Clear-PackageCache {
    [CmdletBinding()]
    param()
    Trace-Log 'Cleaning package cache (except the web cache)'

    # Possible caches to clear are:
    #   all | http-cache | packages-cache | global-packages | temp

    #& $NuGetExe locals http-cache -clear -verbosity detailed
    & $NuGetExe locals packages-cache -clear -verbosity detailed
    #& $NuGetExe locals global-packages -clear -verbosity detailed
    & $NuGetExe locals temp -clear -verbosity detailed
}

Function Restore-SolutionPackages{
    [CmdletBinding()]
    param(
        [Alias('path')]
        [string]$SolutionPath,
        [ValidateSet(4, 12, 14, 15)]
        [int]$MSBuildVersion
    )
    $opts = , 'restore'
    if (-not $SolutionPath) {
        $opts += "${RepoRoot}\.nuget\packages.config", '-SolutionDirectory', $RepoRoot
    }
    else {
        $opts += $SolutionPath
    }
    if ($MSBuildVersion) {
        $opts += '-MSBuildVersion', $MSBuildVersion
    }

    if (-not $VerbosePreference) {
        $opts += '-verbosity', 'quiet'
    }

    Trace-Log "Restoring packages @""$RepoRoot"""
    Trace-Log "$NuGetExe $opts"
    & $NuGetExe $opts
    if (-not $?) {
        Error-Log "Restore failed @""$RepoRoot"". Code: ${LASTEXITCODE}"
    }
}

# dotnet restore
Function Restore-Projects($projectFolder) {

    # The restore command is recursive (unlike the build and pack commands)
    # That is options can be a folder to recursively search for project.json files
    $opts = 'restore', $projectFolder

    if (-not $VerbosePreference) {
        $opts += '--verbosity', 'minimal'
    }

    Trace-Log "Restoring packages"
    Trace-Log "$dotnetExe $opts"

    & $DotNetExe $opts

    if (-not $?) {
        Error-Log "Restore failed @""$_"". Code: $LASTEXITCODE"
    }
}

# Find all paths to all folders with an xproj file (build, pack and test commands are not recursive)
Function Find-XProjects($projectFolder) {
    Get-ChildItem $projectFolder -Recurse -Filter '*.xproj' |`
        %{ Get-DirName $_.FullName }
}

Function Run-Tests {
    [CmdletBinding()]
    param(
        [switch]$SkipRestore,
        [string]$Configuration = $DefaultConfiguration
    )

    if (-not $SkipRestore) {
        Restore-Projects $TestFolder
    }

    $xtests = Find-XProjects $TestFolder
    $xtests | Test-Project -Configuration $Configuration
}

# Build production code
Function Build-Packages {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [string]$BuildLabel = $DefaultBuildLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [switch]$SkipRestore
    )

    if (-not $SkipRestore) {
        Restore-Projects $SrcFolder
    }

    $xprojects = Find-XProjects $SrcFolder
    $xprojects | Invoke-DotnetPack -config $Configuration -label $BuildLabel -build $BuildNumber -out $ArtifactsFolder
}

# dotnet pack
Function Invoke-DotnetPack {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$XProjectLocations,
        [Alias('config')]
        [string]$Configuration = $DefaultConfiguration,
        [Alias('label')]
        [string]$BuildLabel,
        [Alias('build')]
        [int]$BuildNumber,
        [Alias('out')]
        [string]$Output
    )
    Begin {
        $BuildNumber = Format-BuildNumber $BuildNumber

        # Setting the Dotnet AssemblyFileVersion
        $env:DOTNET_ASSEMBLY_FILE_VERSION=$BuildNumber
    }
    Process {
        $XProjectLocations | %{
            $opts = @()
            if ($VerbosePreference) {
                $opts += '-v'
            }
            $opts += 'pack', $_, '--configuration', $Configuration

            if ($Output) {
                $opts += '--output', (Join-Path $Output (Split-Path $_ -Leaf))
            }

            if($BuildLabel -ne 'Release') {
                $opts += '--version-suffix', "${BuildLabel}-${BuildNumber}"
            }
            $opts += '--serviceable'

            Trace-Log "$DotNetExe $opts"

            & $DotNetExe $opts

            if (-not $?) {
                Error-Log "Pack failed @""$_"". Code: $LASTEXITCODE"
            }
        }
    }
    End { }
}

# dotnet test
Function Test-Project {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$XProjectLocations,
        [string]$Configuration = $DefaultConfiguration
    )
    Begin {}
    Process {
        $XProjectLocations | Resolve-Path | %{
            Trace-Log "Running tests in ""$_"""

            $directoryName = Split-Path $_ -Leaf

            Push-Location $_

            $opts = @()
            if ($VerbosePreference) {
                $opts += '-v'
            }
            $opts += 'test', '--configuration', $Configuration

            Trace-Log "$DotNetExe $opts"

            & $DotNetExe $opts

            if (-not $?) {
                Error-Log "Tests failed @""$_"" on .NET Core. Code: $LASTEXITCODE"
            }

            Pop-Location
        }
    }
    End {}
}
