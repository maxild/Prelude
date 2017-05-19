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
    },
    new BuildPathSettings
    {
        BuildToolsDir = "./tools"
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
    .IsDependentOn("Show-Info")
    .IsDependentOn("Print-AppVeyor-Environment-Variables")
    .IsDependentOn("Package");

Task("Travis")
    .IsDependentOn("Show-Info")
    .IsDependentOn("Package");

Task("AppVeyor")
    .IsDependentOn("Show-Info")
    .IsDependentOn("Print-AppVeyor-Environment-Variables")
    .IsDependentOn("Package")
    .IsDependentOn("Upload-AppVeyor-Artifacts")
    .IsDependentOn("Publish-GitHub-Release")
    .Finally(() =>
{
    if (publishingError)
    {
        throw new Exception("An error occurred during the publishing of " + parameters.ProjectName + ".  All publishing tasks have been attempted.");
    }
});

Task("CakeScripts")
    .Does(() =>
{
    var dirToDelete = parameters.Paths.Directories.BuildTools.Combine("Maxfire.CakeScripts");
    if (DirectoryExists(dirToDelete))
    {
        DeleteDirectory(dirToDelete, true);
    }
});

Task("ReleaseNotes")
    .IsDependentOn("Create-Release-Notes");

Task("Clean")
    .IsDependentOn("Clear-Artifacts");

Task("Restore")
    .Does(() =>
{
    Information("Restoring packages...");

    DotNetCoreRestore("./", new DotNetCoreRestoreSettings
    {
        Verbose = false,
        Verbosity = DotNetCoreRestoreVerbosity.Minimal
    });

    Information("Package restore was successful!");
});

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

public class TestResult
{
    private readonly string _msg;
    public TestResult(string msg, int exitCode)
    {
        _msg = msg;
        ExitCode = exitCode;
    }

    public int ExitCode { get; private set; }
    public bool Failed { get { return ExitCode != 0; } }
    public string ErrorMessage { get { return Failed ? string.Concat("One or more tests did fail on ", _msg) : string.Empty; } }
}

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    var results = new List<TestResult>();

    foreach (var testPrj in GetFiles(string.Format("{0}/**/project.json", parameters.Paths.Directories.Test)))
    {
        Information("Run tests in {0}", testPrj);

        var testPrjDir = testPrj.GetDirectory();
        var testPrjName = testPrjDir.GetDirectoryName();

        if (IsRunningOnWindows())
        {
            int exitCode = Run("dotnet", string.Format("test {0} --configuration {1}", testPrj, parameters.Configuration));
            FailureHelper.ExceptionOnError(exitCode, string.Format("Failed to run tests on Core CLR in {0}.", testPrjDir));
        }
        else
        {
            // Ideally we would use the 'dotnet test' command to test both netcoreapp1.0 (CoreCLR)
            // and net452 (Mono), but this currently doesn't work due to
            //    https://github.com/dotnet/cli/issues/3073
            int exitCode1 = Run("dotnet", string.Format("test {0} --configuration {1} --framework netcoreapp1.0", testPrj, parameters.Configuration));
            //FailureHelper.ExceptionOnError(exitCode1, string.Format("Failed to run tests on Core CLR in {0}.", testPrjDir));
            results.Add(new TestResult(string.Format("CoreCLR: {0}", testPrjName), exitCode1));

            // Instead we run xUnit.net .NET CLI test runner directly with mono for the net452 target framework

            // Build using .NET CLI
            int exitCode2 = Run("dotnet", string.Format("build {0} --configuration {1} --framework net452", testPrj, parameters.Configuration));
            FailureHelper.ExceptionOnError(exitCode2, string.Format("Failed to build tests on Desktop CLR in {0}.", testPrjDir));

            // Shell() helper does not support running mono, so we glob here
            var dotnetTestXunit = GetFiles(string.Format("{0}/bin/{1}/net452/*/dotnet-test-xunit.exe", testPrjDir, parameters.Configuration)).First();
            var dotnetTestAssembly = GetFiles(string.Format("{0}/bin/{1}/net452/*/{2}.dll", testPrjDir, parameters.Configuration, testPrjName)).First();

            // Run using Mono
            int exitCode3 = Run("mono", string.Format("{0} {1}", dotnetTestXunit, dotnetTestAssembly));
            //FailureHelper.ExceptionOnError(exitCode3, string.Format("Failed to run tests on Desktop CLR in {0}.", testPrjDir));
            results.Add(new TestResult(string.Format("DesktopCLR: {0}", testPrjName), exitCode3));

        }

        if (results.Any(r => r.Failed))
        {
            throw new Exception(
                results.Aggregate(new StringBuilder(), (sb, result) =>
                    sb.AppendFormat("{0}{1}", result.ErrorMessage, Environment.NewLine)).ToString().TrimEnd()
                );
        }

        Information("Tests in {0} was succesful!", testPrj);
    }
});

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
        Information("Build nupkg in {0}", project.GetDirectory());

        DotNetCorePack(project.GetDirectory().FullPath, new DotNetCorePackSettings {
            VersionSuffix = parameters.VersionInfo.VersionSuffix,
            Configuration = parameters.Configuration,
            OutputDirectory = parameters.Paths.Directories.Artifacts,
            NoBuild = true,
            Verbose = false
        });
    }
});

Task("Package2")
    .IsDependentOn("Clear-Artifacts")
    .IsDependentOn("Test2")
    .IsDependentOn("Copy-Artifacts");

///////////////////////////////////////////////////////////////////////////////
// SECONDARY TASKS (indirect targets)
///////////////////////////////////////////////////////////////////////////////

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
    .WithCriteria(() => parameters.ConfigurationIsRelease())
    .Does(() =>
{
    if (DirectoryExists(parameters.Paths.Directories.Artifacts))
    {
        // TODO: Make this library specific
        // Add ffv-rtl.exe artifact to the published release
        //var exeFile = GetFiles(parameters.Paths.Directories.Artifacts + "/*.exe").Single();
        //GitReleaseManagerAddAssets(parameters.GitHub.UserName, parameters.GitHub.Password,
        //                            parameters.GitHub.RepositoryOwner, parameters.GitHub.RepositoryName,
        //                            parameters.VersionInfo.Milestone, exeFile.FullPath);
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

// appveyor PushArtifact <path> [options] (See https://www.appveyor.com/docs/build-worker-api/#push-artifact)
Task("Upload-AppVeyor-Artifacts")
    .IsDependentOn("Package")
    .WithCriteria(() => parameters.IsRunningOnAppVeyor)
    .WithCriteria(() => DirectoryExists(parameters.Paths.Directories.Artifacts))
    .WithCriteria(() => parameters.ConfigurationIsRelease())
    .Does(() =>
{
    // TODO: Make this library specific
    //var exeFile = GetFiles(parameters.Paths.Directories.Artifacts + "/*.exe").Single();
    //AppVeyor.UploadArtifact(exeFile);
});

Task("Clear-Artifacts")
    .Does(() =>
{
    if (DirectoryExists(parameters.Paths.Directories.Artifacts))
    {
        DeleteDirectory(parameters.Paths.Directories.Artifacts, true);
    }
});

Task("Copy-Artifacts")
    .Does(() =>
{
    EnsureDirectoryExists(parameters.Paths.Directories.Artifacts);
    // TODO: Make this library specific
    //CopyFileToDirectory(
    //    parameters.SrcProject("FFV-RTL.Console").GetBuildArtifact(string.Format("{0}.exe", parameters.ProjectName.ToLower())),
    //    parameters.Paths.Directories.Artifacts);
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
        System.IO.File.WriteAllText(parameters.Paths.Files.CommonAssemblyInfo.FullPath, content, Encoding.UTF8);
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
