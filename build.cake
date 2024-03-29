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
DotNetMSBuildSettings msBuildSettings = null;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    if (parameters.Git.IsMasterBranch && context.Log.Verbosity != Verbosity.Diagnostic)
    {
        Information("Increasing verbosity to diagnostic.");
        context.Log.Verbosity = Verbosity.Diagnostic;
    }

    msBuildSettings = new DotNetMSBuildSettings()
                        .WithProperty("RepositoryBranch", parameters.Git.Branch)        // gitflow branch
                        .WithProperty("RepositoryCommit", parameters.Git.Sha)           // full sha
                        //.WithProperty("Version", parameters.VersionInfo.SemVer)       // semver 2.0 compatible
                        .WithProperty("Version", parameters.VersionInfo.NuGetVersion)   // padded with zeros, because of lexical nuget sort order
                        .WithProperty("AssemblyVersion", parameters.VersionInfo.AssemblyVersion)
                        .WithProperty("FileVersion", parameters.VersionInfo.AssemblyFileVersion);
                        //.WithProperty("PackageReleaseNotes", string.Concat("\"", releaseNotes, "\""));

    Information("Building version {0} of {1} ({2}, {3}) using version {4} of Cake and '{5}' of GitVersion. (IsTagPush: {6})",
        parameters.VersionInfo.SemVer,
        parameters.ProjectName,
        parameters.Configuration,
        parameters.Target,
        parameters.VersionInfo.CakeVersion,
        parameters.VersionInfo.GitVersionVersion,
        parameters.IsTagPush);
});

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TASKS (direct targets)
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Package");

Task("Setup")
    .IsDependentOn("Generate-CommonAssemblyInfo");

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
    var settings = new DotNetRestoreSettings
    {
        Verbosity = DotNetVerbosity.Minimal
    };
    if (parameters.IsLocalBuild)
    {
        // Unable to load the service index for source https://www.myget.org/F/brf/api/v3/index.json,
        // or whatever. Give me a chance to store PAT in credential provider store.
        settings.Interactive = true;
    }

    DotNetRestore(parameters.Paths.Files.Solution.FullPath, settings);
});

Task("Build")
    .IsDependentOn("Generate-CommonAssemblyInfo")
    .IsDependentOn("Restore")
    .Does(() =>
{
    DotNetBuild(parameters.Paths.Files.Solution.FullPath, new DotNetBuildSettings()
    {
        Configuration = parameters.Configuration,
        NoRestore = true,
        MSBuildSettings = msBuildSettings
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    // Only testable projects (<IsTestProject>true</IsTestProject>) will be test-executed
    // We do not need to exclude everything under 'src/submodules',
    // because we use the single master solution
    DotNetTest(parameters.Paths.Files.Solution.FullPath, new DotNetTestSettings
    {
        NoBuild = true,
        NoRestore = true,
        Configuration = parameters.Configuration
    });

    // NOTE: .NET Framework / Mono (net472 on *nix and Mac OSX)
    // ========================================================
    // Microsoft does not officially support Mono via .NET Core SDK. Their support for .NET Core
    // on Linux and OS X starts and ends with .NET Core. Anyway we test on Mono for now, and maybe
    // remove Mono support soon.
    //
    // For Mono to support dotnet-xunit we have to put { "appDomain": "denied" } in config
    // See https://github.com/xunit/xunit/issues/1357#issuecomment-314416426
});

Task("Package")
    .IsDependentOn("Clear-Artifacts")
    .IsDependentOn("Test")
    .Does(() =>
{
    // Only packable projects will produce nupkg's
    var projects = GetFiles($"{parameters.Paths.Directories.Src}/**/*.csproj");
    foreach(var project in projects)
    {
        DotNetPack(project.FullPath, new DotNetPackSettings
        {
            Configuration = parameters.Configuration,
            OutputDirectory = parameters.Paths.Directories.Artifacts,
            NoBuild = true,
            NoRestore = true,
            MSBuildSettings = msBuildSettings
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
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        // appveyor PushArtifact <path> [options] (See https://www.appveyor.com/docs/build-worker-api/#push-artifact)
        AppVeyor.UploadArtifact(package);
    }
});

// Debug builds are published to CI feed
Task("Publish-CIFeed-MyGet")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.ConfigurationIsDebug)
    .WithCriteria(() => parameters.ShouldDeployToCIFeed)
    .Does(() =>
{
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
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

// Release builds are published to production feed
Task("Publish-ProdFeed-NuGet")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.ConfigurationIsRelease)
    .WithCriteria(() => parameters.ShouldDeployToProdFeed)
    .Does(() =>
{
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
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
    GitReleaseManagerCreate(parameters.GitHub.GetRequiredToken(),
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
    foreach (var package in GetFiles(parameters.Paths.Directories.Artifacts + "/*.nupkg"))
    {
        GitReleaseManagerAddAssets(parameters.GitHub.GetRequiredToken(),
                                   parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
                                   parameters.VersionInfo.Milestone, package.FullPath);
    }

    // Close the milestone
    GitReleaseManagerClose(parameters.GitHub.GetRequiredToken(),
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
    parameters.ClearArtifacts();
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

Task("Generate-CommonAssemblyInfo")
    .Does(() =>
{
    // No heredocs in c#, so using verbatim string (cannot use $"", because of Cake version)
    string template = @"using System.Reflection;

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a Cake.
// </auto-generated>
//------------------------------------------------------------------------------

[assembly: System.CLSCompliant(true)]

[assembly: AssemblyProduct(""Maxfire.Prelude"")]
[assembly: AssemblyVersion(""{0}"")]
[assembly: AssemblyFileVersion(""{1}"")]
[assembly: AssemblyInformationalVersion(""{2}"")]
[assembly: AssemblyCopyright(""Copyright (c) Morten Maxild."")]

#if DEBUG
[assembly: AssemblyConfiguration(""Debug"")]
#else
[assembly: AssemblyConfiguration(""Release"")]
#endif";

    string content = string.Format(template,
        parameters.VersionInfo.AssemblyVersion,
        parameters.VersionInfo.AssemblyFileVersion,
        parameters.VersionInfo.AssemblyInformationalVersion);

    // Generate ./src/CommonAssemblyInfo.cs that is ignored by GIT
    System.IO.File.WriteAllText(parameters.Paths.Files.CommonAssemblyInfo.FullPath, content, Encoding.UTF8);
});

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(parameters.Target);
