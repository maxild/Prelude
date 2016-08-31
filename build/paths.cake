public class BuildSettings
{
    public string ArtifactsFolder { get; set; }
    public string SrcFolder { get; set; }
    public string TestFolder { get; set; }
    public string BuildToolsFolder { get; set; }
    public string BuildScriptsFolder { get; set; }
    public bool UseSystemDotNetPath { get; set; }
    public string DotNetCliFolder { get; set; }
    public string DotNetCliInstallScriptUrl { get; set; }
    public string DotNetCliBranch { get; set; }
    public string DotNetCliChannel { get; set; }
    public string DotNetCliVersion { get; set; }
}

public class BuildPaths {
    private readonly BuildSettings _settings;
    private bool _isRunningOnWindows;
    private BuildPaths(ICakeContext context, BuildSettings settings) {
        _settings = settings;
        _isRunningOnWindows = context.IsRunningOnWindows();
    }
    public string Root { get { return System.IO.Directory.GetCurrentDirectory(); } }
    public string Artifacts { get { return System.IO.Path.Combine(Root, _settings.ArtifactsFolder); } }
    public string Src { get { return System.IO.Path.Combine(Root, _settings.SrcFolder); } }
    public string Test { get { return System.IO.Path.Combine(Root, _settings.TestFolder); } }
    public string BuildTools { get { return System.IO.Path.Combine(Root, _settings.BuildToolsFolder); } }
    public string BuildScripts { get { return System.IO.Path.Combine(Root, _settings.BuildScriptsFolder); } }
    public string DotNetCli { get { return System.IO.Path.Combine(Root, _settings.DotNetCliFolder); } }

    public string DotNetToolPath
    {
        get
        {
            if (_settings.UseSystemDotNetPath)
            {
                return null; // Use system dotnet SDK configured in PATH env var
            }

            // Use local dotnet SDK installed in .dotnet subfolder
            return _isRunningOnWindows
                ? System.IO.Path.Combine(DotNetCli, "dotnet.exe")
                : System.IO.Path.Combine(DotNetCli, "dotnet");
        }
    }

    public static BuildPaths GetPaths(ICakeContext context, BuildSettings settings)
    {
        return new BuildPaths(context, settings);
    }
}
