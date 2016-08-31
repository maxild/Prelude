// public class BuildCredentials
// {
//     public string UserName { get; private set; }
//     public string Password { get; private set; }

//     public BuildCredentials(string userName, string password)
//     {
//         UserName = userName;
//         Password = password;
//     }

//     public static BuildCredentials GetGitHubCredentials(ICakeContext context)
//     {
//         return new BuildCredentials(
//             context.EnvironmentVariable("CAKE_GITHUB_USERNAME"),
//             context.EnvironmentVariable("CAKE_GITHUB_PASSWORD"));
//     }
// }

public class BuildParameters
{
    public string Target { get; private set; }
    public string Configuration { get; private set; }
    public bool IsLocalBuild { get; private set; }
    public bool IsRunningOnUnix { get; private set; }
    public bool IsRunningOnWindows { get; private set; }
    public bool IsRunningOnAppVeyor { get; private set; }
    //public bool IsPullRequest { get; private set; }
    //public bool IsMainCakeRepo { get; private set; }
    //public bool IsMainCakeBranch { get; private set; }
    //public bool IsCoreClrBranch { get; private set; }
    public bool IsTagged { get; private set; }
    //public bool IsPublishBuild { get; private set; }
    //public bool IsReleaseBuild { get; private set; }
    //public bool SkipGitVersion { get; private set; }
    //public BuildCredentials GitHub { get; private set; }
    //public ReleaseNotes ReleaseNotes { get; private set; }
    //public BuildVersion Version { get; private set; }
    //public BuildPaths Paths { get; private set; }
    //public BuildPackages Packages { get; private set; }

    public static BuildParameters GetParameters(ICakeContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException("context");
        }

        var target = context.Argument("target", "Default");
        var buildSystem = context.BuildSystem();

        return new BuildParameters {
            Target = target,
            Configuration = context.Argument("configuration", "Release"),
            IsLocalBuild = buildSystem.IsLocalBuild,
            IsRunningOnUnix = context.IsRunningOnUnix(),
            IsRunningOnWindows = context.IsRunningOnWindows(),
            IsRunningOnAppVeyor = buildSystem.AppVeyor.IsRunningOnAppVeyor,
            //IsPullRequest = buildSystem.AppVeyor.Environment.PullRequest.IsPullRequest,
            //IsMainCakeRepo = StringComparer.OrdinalIgnoreCase.Equals("cake-build/cake", buildSystem.AppVeyor.Environment.Repository.Name),
            //IsMainCakeBranch = StringComparer.OrdinalIgnoreCase.Equals("main", buildSystem.AppVeyor.Environment.Repository.Branch),
            //IsCoreClrBranch = StringComparer.OrdinalIgnoreCase.Equals("coreclr", buildSystem.AppVeyor.Environment.Repository.Branch),
            IsTagged = IsBuildTagged(buildSystem),
            //GitHub = BuildCredentials.GetGitHubCredentials(context),
            // TODO: Look into ReleaseNotes.md and using the gitreleasemanager tool
            //ReleaseNotes = context.ParseReleaseNotes("./ReleaseNotes.md"),
            //IsPublishBuild = IsPublishing(target),
            //IsReleaseBuild = IsReleasing(target),
            //SkipGitVersion = StringComparer.OrdinalIgnoreCase.Equals("True", context.EnvironmentVariable("CAKE_SKIP_GITVERSION"))
        };
    }

    private static bool IsBuildTagged(BuildSystem buildSystem)
    {
        return buildSystem.AppVeyor.Environment.Repository.Tag.IsTag
            && !string.IsNullOrWhiteSpace(buildSystem.AppVeyor.Environment.Repository.Tag.Name);
    }

    // private static bool IsReleasing(string target)
    // {
    //     var targets = new [] { "Publish", "Publish-NuGet", "Publish-Chocolatey", "Publish-HomeBrew", "Publish-GitHub-Release" };
    //     return targets.Any(t => StringComparer.OrdinalIgnoreCase.Equals(t, target));
    // }

    // private static bool IsPublishing(string target)
    // {
    //     var targets = new [] { "ReleaseNotes", "Create-Release-Notes" };
    //     return targets.Any(t => StringComparer.OrdinalIgnoreCase.Equals(t, target));
    // }
}

