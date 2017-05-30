///////////////////////////////////////////////////////////////////////////////
// TOOLS
///////////////////////////////////////////////////////////////////////////////
#tool "nuget:?package=gitreleasemanager&version=0.6.0"
#tool "nuget:?package=xunit.runner.console&version=2.1.0"

///////////////////////////////////////////////////////////////////////////////
// SCRIPTS
///////////////////////////////////////////////////////////////////////////////
#load "./tools/Maxfire.CakeScripts/content/all.cake"

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var parameters = CakeScripts.GetParameters(
    Context,            // ICakeContext
    BuildSystem,        // BuildSystem alias
    new BuildSettings   // My personal overrides
    {
        MainRepositoryOwner = "maxild",
        RepositoryName = "Prelude",
        DeployToCIFeedUrl = "https://www.myget.org/F/maxfire-ci/api/v2/package", // MyGet feed url
        DeployToProdFeedUrl = "https://www.nuget.org/api/v2/package"             // NuGet.org feed url
    });
bool publishingError = false;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    if (parameters.Git.IsMasterBranch && context.Log.Verbosity != Verbosity.Diagnostic) {
        Information("Increasing verbosity to diagnostic.");
        context.Log.Verbosity = Verbosity.Diagnostic;
    }

    Information("Building version {0} of {1} ({2}, {3}) using version {4} of Cake. (IsTagPush: {5})",
        parameters.VersionInfo.SemVer,
        parameters.ProjectName,
        parameters.Configuration,
        parameters.Target,
        parameters.VersionInfo.CakeVersion,
        parameters.IsTagPush);
});

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TASKS (direct targets)
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Package");

Task("Travis")
    .IsDependentOn("Show-Info")
    .IsDependentOn("Test");

Task("AppVeyor")
    .IsDependentOn("Show-Info")
    .IsDependentOn("Print-AppVeyor-Environment-Variables")
    .IsDependentOn("Package")
    .IsDependentOn("Upload-AppVeyor-Artifacts")
    .IsDependentOn("Publish")
    .IsDependentOn("Publish-GitHub-Release")
    .Finally(() =>
{
    if (publishingError)
    {
        throw new Exception("An error occurred during the publishing of " + parameters.ProjectName + ".  All publishing tasks have been attempted.");
    }
});

Task("ReleaseNotes")
    .IsDependentOn("Create-Release-Notes");

Task("Clean")
    .IsDependentOn("Clear-Artifacts");

Task("Restore")
    .Does(() =>
{
    DotNetCoreRestore("./", new DotNetCoreRestoreSettings
    {
        Verbose = false,
        Verbosity = DotNetCoreRestoreVerbosity.Minimal
    });
});

// TODO: Move to Lofus
Task("Restore2")
    .Does(() =>
{
    Information("Restoring packages for {0}...", parameters.Paths.Files.Solution);
    NuGetRestore(parameters.Paths.Files.Solution, new NuGetRestoreSettings { ConfigFile = "./nuget.config" });
    Information("Package restore was successful!");
});

Task("Build")
    .IsDependentOn("Patch-Project-Json")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var project in GetFiles("./**/project.json"))
    {
        DotNetCoreBuild(project.GetDirectory().FullPath, new DotNetCoreBuildSettings {
            VersionSuffix = parameters.VersionInfo.VersionSuffix,
            Configuration = parameters.Configuration
        });
    }
});

// TODO: Move to Lofus
Task("Build2")
    .IsDependentOn("Generate-CommonAssemblyInfo")
    .IsDependentOn("Restore2")
    .Does(() =>
{
    Information("Building {0}", parameters.Paths.Files.Solution);

    MSBuild(parameters.Paths.Files.Solution, settings =>
        settings.SetPlatformTarget(PlatformTarget.MSIL) // AnyCPU
            .WithProperty("TreatWarningsAsErrors", "true")
            .WithProperty("nowarn", @"""1591,1573""") // Missing XML comment for publicly visible type or member, Parameter 'parameter' has no matching param tag in the XML comment
            .WithTarget("Clean")
            .WithTarget("Build")
            .SetConfiguration(parameters.Configuration)
            .SetVerbosity(Verbosity.Minimal)
    );
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    foreach (var testProject in GetFiles(string.Format("{0}/**/project.json", parameters.Paths.Directories.Test)))
    {
        if (IsRunningOnWindows())
        {
            DotNetCoreTest(testProject.GetDirectory().FullPath, new DotNetCoreTestSettings {
                Configuration = parameters.Configuration,
                NoBuild = true,
                Verbose = false
            });
        }
        else
        {
            // Ideally we would use the 'dotnet test' command to test both netcoreapp1.0 (CoreCLR)
            // and net452 (Mono), but this currently doesn't work due to
            //    https://github.com/dotnet/cli/issues/3073

            //
            // .NET Core (on Linux and OS X)
            //

            DotNetCoreTest(testProject.GetDirectory().FullPath, new DotNetCoreTestSettings {
                Configuration = parameters.Configuration,
                Framework = "netcoreapp1.0",
                NoBuild = true,
                Verbose = false
            });

            //
            // Mono (on Linux and OS X)
            //

            var testProjectPath = testProject.GetDirectory().FullPath;
            var testProjectName = testProject.GetDirectory().GetDirectoryName();

            var xunitRunner = GetFiles(testProjectPath + "/bin/" + parameters.Configuration + "/net452/*/dotnet-test-xunit.exe").First().FullPath;
            var testAssembly = GetFiles(testProjectPath + "/bin/" + parameters.Configuration + "/net452/*/" + testProjectName + ".dll").First().FullPath;

            int exitCode = Run("mono", xunitRunner + " " + testAssembly);
            if (exitCode != 0)
            {
                throw new Exception("Tests in '" + testProjectName + "' failed on Mono!");
            }

        }
    }
});

// TODO: Move to Lofus
Task("Test2")
    .IsDependentOn("Build2")
    .Does(() =>
{
    var testAssemblies = parameters.GetBuildArtifacts("Brf.Lofus.Core.Tests", "Brf.Lofus.Integration.Tests", "Brf.Lofus.ProductSpecs");
    Information("Running tests for {0}", string.Join(", ", testAssemblies));
    XUnit2(testAssemblies);
});

Task("Package")
    .IsDependentOn("Clear-Artifacts")
    .IsDependentOn("Test")
    .Does(() =>
{
    foreach (var project in GetFiles(string.Format("{0}/**/project.json", parameters.Paths.Directories.Src)))
    {
        DotNetCorePack(project.GetDirectory().FullPath, new DotNetCorePackSettings {
            VersionSuffix = parameters.VersionInfo.VersionSuffix,
            Configuration = parameters.Configuration,
            OutputDirectory = parameters.Paths.Directories.Artifacts,
            NoBuild = true,
            Verbose = false
        });
    }
});

Task("Publish")
    .IsDependentOn("Publish-CIFeed-MyGet")
    .IsDependentOn("Publish-ProdFeed-NuGet");

Task("Upload-AppVeyor-Artifacts")
    .IsDependentOn("Upload-AppVeyor-Debug-Artifacts")
    .IsDependentOn("Upload-AppVeyor-Release-Artifacts");

///////////////////////////////////////////////////////////////////////////////
// SECONDARY TASKS (indirect targets)
///////////////////////////////////////////////////////////////////////////////

// Release artifacts are uploaded for release-line branches (master and support), and Debug
// artifacts are uploaded for non release-line branches (dev, feature etc.).
Task("Upload-AppVeyor-Debug-Artifacts")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.IsRunningOnAppVeyor)
    .WithCriteria(() => parameters.Git.IsDevelopmentLineBranch && parameters.ConfigurationIsDebug)
    .Does(() =>
{
    // TODO: Both NAME.nupkg and NAME.symbols.nupkg?
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        // appveyor PushArtifact <path> [options] (See https://www.appveyor.com/docs/build-worker-api/#push-artifact)
        AppVeyor.UploadArtifact(package);
    }
});

Task("Upload-AppVeyor-Release-Artifacts")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.IsRunningOnAppVeyor)
    .WithCriteria(() => parameters.Git.IsReleaseLineBranch && parameters.ConfigurationIsRelease)
    .Does(() =>
{
    // TODO: Both NAME.nupkg and NAME.symbols.nupkg?
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        // appveyor PushArtifact <path> [options] (See https://www.appveyor.com/docs/build-worker-api/#push-artifact)
        AppVeyor.UploadArtifact(package);
    }
});

// Debug builds are published to MyGet CI feed
Task("Publish-CIFeed-MyGet")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.ConfigurationIsDebug)
    .WithCriteria(() => parameters.ShouldDeployToCIFeed)
    .Does(() =>
{
    var packages =
            GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg") -
            GetFiles(parameters.Paths.Directories.Artifacts + "/*.symbols.nupkg");

    foreach (var package in packages)
    {
        NuGetPush(package.FullPath, new NuGetPushSettings {
            Source = parameters.CIFeed.SourceUrl,
            ApiKey = parameters.CIFeed.ApiKey,
            ArgumentCustomization = args => args.Append("-NoSymbols")
        });
    }
})
.OnError(exception =>
{
    Information("Publish-MyGet Task failed, but continuing with next Task...");
    publishingError = true;
});

// Release builds are published to NuGet.Org production feed
Task("Publish-ProdFeed-NuGet")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.ConfigurationIsRelease)
    .WithCriteria(() => parameters.ShouldDeployToProdFeed)
    .Does(() =>
{
    // Note: NuGet automatically publishes to symbolsource.org automatically if it detects a symbol package
    // See also https://docs.microsoft.com/en-us/nuget/create-packages/symbol-packages

    var packages =
            GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg") -
            GetFiles(parameters.Paths.Directories.Artifacts + "/*.symbols.nupkg");

    foreach (var package in packages)
    {
        NuGetPush(package.FullPath, new NuGetPushSettings {
            Source = parameters.ProdFeed.SourceUrl,
            ApiKey = parameters.ProdFeed.ApiKey,
            ArgumentCustomization = args => args.Append("-NoSymbols")
        });
    }
})
.OnError(exception =>
{
    Information("Publish-NuGet Task failed, but continuing with next Task...");
    publishingError = true;
});

Task("Create-Release-Notes")
    .Does(() =>

{
    // This is both the title and tagName of the release (title can be edited on github.com)
    string milestone = Environment.GetEnvironmentVariable("GitHubMilestone") ??
                       parameters.VersionInfo.Milestone;
    Information("Creating draft release of version '{0}' on GitHub", milestone);
    GitReleaseManagerCreate(parameters.GitHub.UserName, parameters.GitHub.Password,
                            parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
        new GitReleaseManagerCreateSettings
        {
            Milestone         = milestone,
            Prerelease        = false,
            TargetCommitish   = "master"
        });
});

// Invoked on AppVeyor after draft release have been published on github.com
Task("Publish-GitHub-Release")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.ShouldDeployToProdFeed)
    .WithCriteria(() => parameters.ConfigurationIsRelease)
    .Does(() =>
{
    // TODO: Both NAME.nupkg and NAME.symbols.nupkg?
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        GitReleaseManagerAddAssets(parameters.GitHub.UserName, parameters.GitHub.Password,
                                   parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
                                   parameters.VersionInfo.Milestone, package.FullPath);

    }

    // Close the milestone
    GitReleaseManagerClose(parameters.GitHub.UserName, parameters.GitHub.Password,
                           parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
                           parameters.VersionInfo.Milestone);
})
.OnError(exception =>
{
    Information("Publish-GitHub-Release Task failed, but continuing with next Task...");
    publishingError = true;
});

Task("Clear-Artifacts")
    .Does(() =>
{
    if (DirectoryExists(parameters.Paths.Directories.Artifacts))
    {
        DeleteDirectory(parameters.Paths.Directories.Artifacts, true);
    }
});

Task("Show-Info")
    .Does(() =>
{
    parameters.PrintToLog();
});

Task("Print-AppVeyor-Environment-Variables")
    .WithCriteria(AppVeyor.IsRunningOnAppVeyor)
    .Does(() =>
{
    parameters.PrintAppVeyorEnvironmentVariables();
});


Task("Patch-Project-Json")
    .Does(() =>
{
    // Only production code is patched
    var projects = GetFiles("./src/**/project.json");

    foreach (var project in projects)
    {
        Information("Patching project.json in '{0}' to have version equal to {1}",
            project.GetDirectory().GetDirectoryName(),
            parameters.VersionInfo.NuGetVersion);

        // Reads the current version without the '-*' suffix
        string currVersion = ProjectJsonUtil.ReadProjectJsonVersion(project.FullPath);

        Information("The version in the project.json is {0}", currVersion);

        // Only patch project.json files if the major.minor.patch versions do not match
        if (parameters.VersionInfo.MajorMinorPatch != currVersion) {

            Information("Patching version to {0}", parameters.VersionInfo.PatchedVersion);

            if (!ProjectJsonUtil.PatchProjectJsonVersion(project, parameters.VersionInfo.PatchedVersion))
            {
                Warning("No version specified in {0}.", project.FullPath);
            }
        }
    }
});

// TODO: Den benyttes ikke...afventer investigation af tooling (project.json vs csproj, and assemblyinfo attributes)
// TODO: Skal denne tilfoejes vs2015 solution (dvs xproj), saa skal den kopieres under src, da filer adderes dynamisk
// TODO: Hvordan indsaettes AssemblyVersion, AssemblyFileVersion og AssemblyInformationalVersion
Task("Generate-CommonAssemblyInfo")
    .Does(() =>
{
    // No heredocs in c#, so using verbatim string (cannot use $"", because of Cake version)
    string template = @"using System.Reflection;

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.4927
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

[assembly: AssemblyCompany(""Maxfire"")]
[assembly: AssemblyProduct(""Maxfire.Prelude"")]
[assembly: AssemblyCopyright(""Copyright (c) Morten Maxild."")]

[assembly: AssemblyVersion(""{0}"")]
[assembly: AssemblyFileVersion(""{1}"")]
[assembly: AssemblyInformationalVersion(""{2}"")]

#if DEBUG
[assembly: AssemblyConfiguration(""Debug"")]
#else
[assembly: AssemblyConfiguration(""Release"")]
#endif";

    string content = string.Format(template,
        parameters.VersionInfo.AssemblyVersion,
        parameters.VersionInfo.AssemblyFileVersion,
        parameters.VersionInfo.AssemblyInformationalVersion);

    // Only production code is assembly version patched
    var projects = GetFiles("./src/**/project.json");
    foreach (var project in projects)
    {
        // TODO: Indsaettes som ./Properties/AssemblyVersionInfo.cs i hvert projekt
        System.IO.File.WriteAllText(parameters.Paths.Files.CommonAssemblyInfo.FullPath, content, Encoding.UTF8);
        //System.IO.File.WriteAllText(System.IO.Path.Combine(parameters.Paths.Directories.Src, "Maxfire.Prelude", "Properties" , "AssemblyVersionInfo.cs"), content, Encoding.UTF8);
        //System.IO.File.WriteAllText(System.IO.Path.Combine(project.GetDirectory().FullPath, "Properties" , "AssemblyVersionInfo.cs"), content, Encoding.UTF8);
    }
});

Task("Clear-PackageCache")
    .Does(() =>
{
    Information("Clearing NuGet package caches...");

    // NuGet restore with single source (nuget.org v3 feed) reports
    //    Feeds used:
    //        %LOCALAPPDATA%\NuGet\Cache          (packages-cache)
    //        C:\Users\Maxfire\.nuget\packages\   (global-packages)
    //        https://api.nuget.org/v3/index.json (only configured feed)

    var nugetCaches = new Dictionary<string, bool>
    {
        {"http-cache", false},      // %LOCALAPPDATA%\NuGet\v3-cache
        {"packages-cache", true},   // %LOCALAPPDATA%\NuGet\Cache
        {"global-packages", true},  // ~\.nuget\packages\
        {"temp", false},            // %LOCALAPPDATA%\Temp\NuGetScratch
    };

    var nuget = parameters.Paths.Tools.NuGet.FullPath;

    foreach (var cache in nugetCaches.Where(kvp => kvp.Value).Select(kvp => kvp.Key))
    {
        Information("Clearing nuget resources in {0}.", cache);
        int exitCode = Run(nuget, string.Format("locals {0} -clear -verbosity detailed", cache));
        FailureHelper.ExceptionOnError(exitCode, string.Format("Failed to clear nuget {0}.", cache));
    }

    Information("NuGet package cache clearing was succesful!");
});

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(parameters.Target);
