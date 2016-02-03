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
$ValidReleaseLabels = 'Release','rtm', 'rc', 'beta', 'local' # TODO: Not used!!
$DefaultReleaseLabel = 'local'

# The following 2 DNX versions are the defaults on the stable and unstable feeds as of today
$DefaultDnxVersion = '1.0.0-rc1-update1'
$DefaultUnstableDnxVersion = '1.0.0-rc2-16357'
$DefaultDnxArch = 'x86'
$DnvmCmd = Join-Path $env:USERPROFILE '.dnx\bin\dnvm.cmd'

$RepoRoot = Get-DirName $PSScriptRoot
$NuGetExe = Join-Path $RepoRoot '.nuget\nuget.exe'
$ArtifactsFolder = Join-Path $RepoRoot artifacts

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

# Downloads NuGet.exe if missing
Function Install-NuGet {
    [CmdletBinding()]
    param(
        [string] $NugetVersion = "latest"
    )
    if (-not (Test-File $NuGetExe)) {
        Trace-Log 'Downloading nuget.exe'
        Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/$NugetVersion/nuget.exe" -OutFile $NuGetExe
    }
}

# Validates DNVM installed and installs it if missing
Function Install-DNVM {
    [CmdletBinding()]
    param()
    if (-not (Test-File $DnvmCmd)) {
        Trace-Log 'Downloading DNVM'
        # See also: https://docs.asp.net/en/latest/getting-started/installing-on-windows.html#install-asp-net-5-from-the-command-line
        &{
            $Branch='dev'
            iex (`
                (new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1')`
            )
        }
    }
}

# Makes sure the needed DNX runtimes installed
Function Install-DNX {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True, Position=0)]
        [Alias('r')]
        [ValidateSet('CLR', 'CoreCLR')]
        [string]$Runtime,
        [Alias('v')]
        [string]$Version,
        [Alias('a')]
        [string]$Arch = $DefaultDnxArch,
        [switch]$Default,
        [Alias('u')]
        [switch]$Unstable
    )
    Install-DNVM
    $env:DNX_FEED = 'https://www.nuget.org/api/v2'
    $env:DNX_UNSTABLE_FEED = 'https://www.myget.org/F/aspnetvnext/api/v2'
    $resolvedVersion = Resolve-DnxVersion -v $Version -u:$Unstable
    if ($Unstable) {
        Verbose-Log "dnvm install $resolvedVersion -u -runtime $Runtime -arch $Arch"
        if ($Default) {
            & dnvm install $resolvedVersion -u -runtime $Runtime -arch $Arch -alias default 2>&1
        }
        else {
            & dnvm install $resolvedVersion -u -runtime $Runtime -arch $Arch 2>&1
        }
    }
    else {
        Verbose-Log "dnvm install $resolvedVersion -runtime $Runtime -arch $Arch"
        if ($Default) {
            & dnvm install $resolvedVersion -runtime $Runtime -arch $Arch -alias default 2>&1
        }
        else {
            & dnvm install $resolvedVersion -runtime $Runtime -arch $Arch 2>&1
        }
    }
}

Function Use-DNX {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True, Position=0)]
        [Alias('r')]
        [ValidateSet('CLR', 'CoreCLR')]
        [string]$Runtime,
        [Alias('v')]
        [string]$Version,
        [Alias('a')]
        [string]$Arch = $DefaultDnxArch,
        [Alias('u')]
        [switch]$Unstable
    )
    $resolvedVersion = Resolve-DnxVersion -v $Version -u:$Unstable
    Verbose-Log "dnvm use $resolvedVersion -runtime $Runtime -arch $Arch"
    & dnvm use $resolvedVersion -runtime $Runtime -arch $Arch 2>&1
}

Function Resolve-DnxVersion {
    [CmdletBinding()]
    param(
        [Alias('v')]
        [string]$Version,
        [Alias('u')]
        [switch]$Unstable
    )
    if ($Version) {
        return $Version
    }
    if ($Unstable) {
        $DefaultUnstableDnxVersion
    }
    else {
        $DefaultDnxVersion
    }
}

# Get the sdk version from global.json
# TODO: should allow empty to be default alias
Function Get-DnxVersion
{
    $globalJson = join-path $PSScriptRoot "global.json"
    $jsonData = Get-Content -Path $globalJson -Raw | ConvertFrom-JSON
    return $jsonData.sdk.version
}

# Local builds will generate a build number based on the 'duration' since semantic version date
Function Get-BuildNumber() {
    $SemanticVersionDate = '2015-11-30'
    [int](((Get-Date) - (Get-Date $SemanticVersionDate)).TotalMinutes / 5)
}

# D4 means 'pad left with 0000', 1 -> '0001', 2 -> '0002',..., 12345 -> '12345' etc.
Function Format-BuildNumber([int]$BuildNumber) {
    '{0:D4}' -f $BuildNumber
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

# Clean the machine level cache from all packages (better to be safe than sorry)
#          * Note: It is possible to run DNU with --no-cache switch
#          * Note: DNU clear-http-cache command will clear the package cache
Function Clear-PackageCache {
    [CmdletBinding()]
    param()

    Trace-Log 'Removing .DNX packages'

    # wipe out ~\.dnx\packages
    if (Test-Dir $env:userprofile\.dnx\packages) {
        rm -r $env:userprofile\.dnx\packages -Force
    }

    Trace-Log 'Removing .NUGET packages'

    # wipe out ~\.nuget\packages
    if (Test-Dir $env:userprofile\.nuget\packages) {
        rm -r $env:userprofile\.nuget\packages -Force
    }

    Trace-Log 'Removing DNU cache'

    # wipe out ~\AppData\Local\dnu\cache (sometimes it can get corrupted)
    if (Test-Dir $env:localappdata\dnu\cache) {
        rm -r $env:localappdata\dnu\cache -Force
    }

    Trace-Log 'Removing NuGet web cache'

    # wipe out ~\AppData\Local\NuGet\v3-cache (sometimes it can get corrupted)
    if (Test-Dir $env:localappdata\NuGet\v3-cache) {
        rm -r $env:localappdata\NuGet\v3-cache -Force
    }

    Trace-Log 'Removing NuGet machine cache'

    # wipe out ~\AppData\Local\NuGet\Cache (a lot of nupkg files)
    if (Test-Dir $env:localappdata\NuGet\Cache) {
        rm -r $env:localappdata\NuGet\Cache -Force
    }
}

# Restore projects individually (dnu restore ../project.json -s sources)
Function Restore-Project {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$ProjectLocations
    )
    Begin {}
    Process {
        $ProjectLocations | %{
            $projectJsonFile = Join-Path $_ 'project.json'
            $opts = 'restore', $projectJsonFile
            $opts += $PackageSources | %{ '-s', $_ }
            if (-not $VerbosePreference) {
                $opts += '--quiet'
            }

            Trace-Log "Restoring packages @""$_"""
            Verbose-Log "dnu $opts"
            & dnu $opts 2>&1
            if (-not $?) {
                Error-Log "Restore failed @""$_"". Code: $LASTEXITCODE"
            }
        }
    }
    End {}
}

# TODO: project.json instead....maybe
# Find all paths to all folders with an xproj file
Function Find-Projects($projectsLocation) {
    Get-ChildItem $projectsLocation -Recurse -Filter '*.xproj' |`
        %{ Get-DirName $_.FullName }
}

# dnu restore all DNX projects
Function Restore-Projects {
    [CmdletBinding()]
    param([string]$projectsLocation)

    $projects = Find-Projects $projectsLocation
    $projects | Restore-Project
}

Function Invoke-DnuPack {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$ProjectLocations,
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

        # FWIW I have no idea why we need to call Format-BuildNumber
        # when re-assigning to Int32 again...this is a noop.
        $BuildNumber = Format-BuildNumber $BuildNumber

        # In project.json we could have: { "version": "1.0.0-*", ...}
        # If you set the DNX_BUILD_VERSION environment variable, it
        # will replace the -* with -{DNX_BUILD_VERSION}.
        # Setting the DNX build version (This will make a pre-release SemVer:
        # 1.0.0-* will become 1.0.0-{BuildLabel}-{BuildNumber})
        if($BuildLabel -ne 'Release') {
            $env:DNX_BUILD_VERSION="${BuildLabel}-${BuildNumber}"
        }

        # Setting the DNX AssemblyFileVersion
        $env:DNX_ASSEMBLY_FILE_VERSION=$BuildNumber

        # TODO: Investigate DNX_BUILD_PORTABLE_PDB envvar (See https://github.com/aspnet/dnx/pull/2609)

        # TODO: We need to put git-sha (commit-id) into dnu pack????

        # TODO: Investigate source indexing pdb files with git commit-id

        # For project.json as { "version": "1.0.0-*", ...}, together with label='build'
        # and build=12345, the end result is something equivalent to:
        #  [assembly: AssemblyVersion("1.0.0.0")]
        #  [assembly: AssemblyFileVersion("1.0.0.12345")]
        #  [assembly: AssemblyInformationalVersion("1.0.0-build-12345")]
    }
    Process {
        $ProjectLocations | %{
            $opts = , 'pack'
            $opts += $_
            $opts += '--configuration', $Configuration
            if ($Output) {
                $opts += '--out', (Join-Path $Output (Get-FileName $_))
            }
            if (-not $VerbosePreference) {
                $opts += '--quiet'
            }

            Verbose-Log "dnu $opts"
            &dnu $opts 2>&1
            if (-not $?) {
                Error-Log "Pack failed @""$_"". Code: $LASTEXITCODE"
            }
        }
    }
    End { }
}

Function Build-Projects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [string]$BuildLabel = $DefaultReleaseLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [switch]$SkipRestore
    )
    # test code is not built here
    $projectsLocation = Join-Path $RepoRoot src

    if (-not $SkipRestore) {
        Restore-Projects $projectsLocation
    }

    # dnu pack will build all nupkgs and place them in ./artifacts folder
    $projects = Find-Projects $projectsLocation
    $projects | Invoke-DnuPack -config $Configuration -label $BuildLabel -build $BuildNumber -out $ArtifactsFolder
}

Function Test-Projects {
    [CmdletBinding()]
    param(
        [switch]$SkipRestore
    )
    $projectsLocation = Join-Path $RepoRoot test

    if (-not $SkipRestore) {
        Restore-Projects $projectsLocation
    }

    $xtests = Find-Projects $projectsLocation
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
