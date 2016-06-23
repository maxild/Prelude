#
# Credit goes to Nuget.Client (See https://github.com/NuGet/NuGet.Client/blob/dev/build/common.ps1)
#

Function Get-FileName {
    param([string]$path)
    Split-Path -Path $path -Leaf
}

Function Get-DirName {
    param([string]$path)
    Split-Path -Path $path -Parent
}

Function Test-Dir {
    param([string]$path)
    Test-Path -PathType Container -Path $path
}

Function Test-File {
    param([string]$path)
    Test-Path -PathType Leaf -Path $path
}

Function Create-Dir {
    param([string]$path)
    New-Item -ItemType directory -Path $path
}

### Constants ###

$ValidConfigurations = 'debug', 'release'
$DefaultConfiguration = 'debug'
$ValidBuildLabels = 'Release','rtm', 'rc', 'beta', 'local' # TODO: Not used!!
$DefaultBuildLabel = 'local'

$RepoRoot = Get-DirName $PSScriptRoot
$ArtifactsFolder = Join-Path $RepoRoot 'artifacts'
$SrcFolder = Join-Path $RepoRoot 'src'
$TestFolder = Join-Path $RepoRoot 'test'

$DotNetCliFolder = Join-Path $RepoRoot '.dotnetcli'
$DotNetExe = Join-Path $DotNetCliFolder 'dotnet.exe'

$MSBuildRoot = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\'
#$MSBuildExeRelPath = 'bin\msbuild.exe'

$NuGetFolder = Join-Path $RepoRoot '.nuget'
$NuGetExe = Join-Path $NuGetFolder 'nuget.exe'

#$XunitConsole = Join-Path $NuGetClientRoot 'packages\xunit.runner.console.2.1.0\tools\xunit.console.exe'
#$ILMerge = Join-Path $NuGetClientRoot 'packages\ILMerge.2.14.1208\tools\ILMerge.exe'

Set-Alias dotnet $DotNetExe
Set-Alias nuget $NuGetExe
#Set-Alias xunit $XunitConsole
#Set-Alias ilmerge $ILMerge

# TODO: We probably don't need <add key="BuildFeed" value="Nupkgs" /> in our
# Nuget.config, so delete BuildFeed below
Function Read-PackageSources {
    param($NuGetConfig)
    $xml = New-Object xml
    $xml.Load($NuGetConfig)
    $xml.SelectNodes('/configuration/packageSources/add') | `
        ? { $_.key -ne "BuildFeed" } | `
        % { $_.value }
}
$PackageSources = Read-PackageSources (Join-Path $RepoRoot 'NuGet.Config')

### Functions ###

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

    $MSBuildExe = Get-MSBuildExe $MSBuildVersion
    Test-Path $MSBuildExe
}

Function Get-MSBuildExe {
    param(
        [string]$MSBuildVersion
    )

    $MSBuildExe = Join-Path $MSBuildRoot ($MSBuildVersion + ".0")
    Join-Path $MSBuildExe $MSBuildExeRelPath
}

# Downloads NuGet.exe if missing
Function Install-NuGet {
    [CmdletBinding()]
    param(
        [string] $NugetVersion = "latest-prerelease"
    )

    # Create .nuget folder if necessary
    New-Item -ItemType Directory -Force -Path $NuGetFolder | Out-Null

    # TODO: What about self-updating???
    if (-not (Test-File $NuGetExe)) {
        Trace-Log 'Downloading nuget.exe'
        Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/$NugetVersion/nuget.exe" -OutFile $NuGetExe
    }
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
    & $DotNetExe --info
}

# Get the sdk version from global.json
Function Get-SdkVersionFromGlobalJson
{
    $repoRoot = Split-Path -Path $PSScriptRoot -Parent
    $globalJson = join-path $repoRoot "global.json"
    $jsonData = Get-Content -Path $globalJson -Raw | ConvertFrom-JSON
    return $jsonData.sdk.version
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

    #& nuget locals http-cache -clear -verbosity detailed
    & nuget locals packages-cache -clear -verbosity detailed
    #& nuget locals global-packages -clear -verbosity detailed
    & nuget locals temp -clear -verbosity detailed
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

# Restore projects individually (dnu restore ../project.json -s sources)
# Function Restore-Project {
#     [CmdletBinding()]
#     param(
#         [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
#         [string[]]$ProjectLocations
#     )
#     Begin {}
#     Process {
#         $ProjectLocations | %{
#             $projectJsonFile = Join-Path $_ 'project.json'
#             $opts = 'restore', $projectJsonFile
#             $opts += $PackageSources | %{ '-s', $_ }
#             if (-not $VerbosePreference) {
#                 $opts += '--quiet'
#             }

#             Trace-Log "Restoring packages @""$_"""
#             Verbose-Log "dnu $opts"
#             & dnu $opts 2>&1
#             if (-not $?) {
#                 Error-Log "Restore failed @""$_"". Code: $LASTEXITCODE"
#             }
#         }
#     }
#     End {}
# }

# Function Restore-Projects {
#     [CmdletBinding()]
#     param([string]$projectsLocation)

#     $projects = Find-XProjects $projectsLocation
#     $projects | Restore-Project
# }

Function Restore-XProjects($projectsLocation) {

    # The restore command is recursive (unlike the build and pack commands)
    # That is options can be a folder to recursively search for project.json files
    $opts = 'restore', $projectsLocation

    if (-not $VerbosePreference) {
        $opts += '--verbosity', 'minimal'
    }

    Trace-Log "Restoring packages for xprojs"
    Trace-Log "$dotnetExe $opts"

    & $DotNetExe $opts

    if (-not $?) {
        Error-Log "Restore failed @""$_"". Code: $LASTEXITCODE"
    }
}

# Find all paths to all folders with an xproj file
Function Find-XProjects($projectsLocation) {
    Get-ChildItem $projectsLocation -Recurse -Filter '*.xproj' |`
        %{ Get-DirName $_.FullName }
}

# Build production code
Function Build-SrcProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [string]$BuildLabel = $DefaultBuildLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [switch]$SkipRestore
    )

    if (-not $SkipRestore) {
        Restore-XProjects $SrcFolder
    }

    $xprojects = Find-XProjects $SrcFolder
    $xprojects | Invoke-DotnetPack -config $Configuration -label $BuildLabel -build $BuildNumber -out $ArtifactsFolder
}

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


# Function Invoke-DnuPack {
#     [CmdletBinding()]
#     param(
#         [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
#         [string[]]$ProjectLocations,
#         [Alias('config')]
#         [string]$Configuration = $DefaultConfiguration,
#         [Alias('label')]
#         [string]$BuildLabel,
#         [Alias('build')]
#         [int]$BuildNumber,
#         [Alias('out')]
#         [string]$Output
#     )
#     Begin {

#         [string]$paddedBuildNumber = Format-BuildNumber $BuildNumber

#         # In project.json we could have: { "version": "1.0.0-*", ...}
#         # If you set the DNX_BUILD_VERSION environment variable, it
#         # will replace the -* with -{DNX_BUILD_VERSION}.
#         # Setting the DNX build version (This will make a pre-release SemVer:
#         # 1.0.0-* will become 1.0.0-{PrereleaseTag}-{BuildNumber})
#         if($PrereleaseTag -ne 'Release') {
#             $env:DNX_BUILD_VERSION="${PrereleaseTag}-${paddedBuildNumber}"
#         }

#         # Setting the DNX AssemblyFileVersion
#         $env:DNX_ASSEMBLY_FILE_VERSION=$paddedBuildNumber

#         # TODO: Investigate DNX_BUILD_PORTABLE_PDB envvar (See https://github.com/aspnet/dnx/pull/2609)

#         # TODO: We need to put git-sha (commit-id) into dnu pack????

#         # TODO: Investigate source indexing pdb files with git commit-id

#         # For project.json as { "version": "1.0.0-*", ...}, together with label='build'
#         # and build=12345, the end result is something equivalent to:
#         #  [assembly: AssemblyVersion("1.0.0.0")]
#         #  [assembly: AssemblyFileVersion("1.0.0.12345")]
#         #  [assembly: AssemblyInformationalVersion("1.0.0-build-12345")]
#     }
#     Process {
#         $ProjectLocations | %{
#             $opts = , 'pack'
#             $opts += $_
#             $opts += '--configuration', $Configuration
#             if ($Output) {
#                 $opts += '--out', (Join-Path $Output (Get-FileName $_))
#             }
#             if (-not $VerbosePreference) {
#                 $opts += '--quiet'
#             }

#             Verbose-Log "dnu $opts"
#             &dnu $opts 2>&1
#             if (-not $?) {
#                 Error-Log "Pack failed @""$_"". Code: $LASTEXITCODE"
#             }
#         }
#     }
#     End { }
# }

# Function Build-Projects {
#     [CmdletBinding()]
#     param(
#         [string]$Configuration = $DefaultConfiguration,
#         [string]$BuildLabel = $DefaultReleaseLabel,
#         [int]$BuildNumber = (Get-BuildNumber),
#         [switch]$SkipRestore
#     )


#     if (-not $SkipRestore) {
#         Restore-Projects $projectsLocation
#     }

#     # dnu pack will build all nupkgs and place them in ./artifacts folder
#     $projects = Find-XProjects $projectsLocation
#     $projects | Invoke-DnuPack -config $Configuration -label $BuildLabel -build $BuildNumber -out $ArtifactsFolder
# }

Function Test-Projects {
    [CmdletBinding()]
    param(
        [switch]$SkipRestore
    )
    $projectsLocation = Join-Path $RepoRoot test

    if (-not $SkipRestore) {
        Restore-Projects $projectsLocation
    }

    $xtests = Find-XProjects $projectsLocation
    $xtests | Test-Project
}

Function Test-Project {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$ProjectLocations
    )
    Begin {
        # Test assemblies should not be signed
        if (Test-Path Env:\DNX_BUILD_KEY_FILE) {
            Remove-Item Env:\DNX_BUILD_KEY_FILE
        }

        if (Test-Path Env:\DNX_BUILD_DELAY_SIGN) {
            Remove-Item Env:\DNX_BUILD_DELAY_SIGN
        }
    }
    Process {
        $ProjectLocations | %{
            Trace-Log "Running tests in ""$_"""

            $opts = '-p', $_, 'test'
            if ($VerbosePreference) {
                $opts += '-diagnostics', '-verbose'
            }
            else {
                $opts += '-nologo', '-quiet'
            }
            Verbose-Log "dnx $opts"

            # Check if dnxcore50 exists in the project.json file
            $xtestProjectJson = Join-Path $_ "project.json"
            if (Get-Content $($xtestProjectJson) | Select-String "dnxcore50") {
                # Run tests for CoreCLR-x64 (New .NET Core 1.0, 64 bit windows)
                Use-DNX -u -r CoreCLR -a x64
                & dnx $opts 2>&1
                if (-not $?) {
                    Error-Log "Tests failed @""$_"" on CoreCLR. Code: $LASTEXITCODE"
                }
            }

            # Run tests for CLR-x64 (Classic .NET Framework 4.x, 64 bit windows)
            Use-DNX -u -r CLR -a x64
            & dnx $opts 2>&1
            if (-not $?) {
                Error-Log "Tests failed @""$_"" on CLR. Code: $LASTEXITCODE"
            }
        }
    }
    End {}
}
