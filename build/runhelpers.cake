/// <summary>
/// Run a shell command (powershell or bash).
/// </summary>
/// <param name="command">The command to execute in the shell</param>
/// <returns>The exit status</returns>
int Shell(string command)
{
    return Shell(command, new ProcessSettings());
}

/// <summary>
/// Run a shell command (powershell or bash).
/// </summary>
/// <param name="command">The command to execute in the shell</param>
/// <param name="workingDirectory">Working directory</param>
/// <returns>The exit status</returns>
int Shell(string command, string workingDirectory)
{
    return Shell(command, new ProcessSettings { WorkingDirectory = workingDirectory });
}

/// <summary>
/// Run a shell command (powershell or bash).
/// </summary>
/// <param name="command">The command to execute in the shell</param>
/// <param name="settings">Optional settings</param>
/// <returns>The exit status</returns>
int Shell(string command, ProcessSettings settings)
{
    if (settings == null) 
    {
        throw new ArgumentNullException("settings");
    }
    var exec = IsRunningOnWindows() ? "powershell" : "bash";
    var args = IsRunningOnWindows() 
        ? "/Command " + command 
        : "-C " + command;
    return Run(exec, args, settings);
}

/// <summary>
///  Run the given executable with the given arguments.
/// </summary>
/// <param name="exec">Executable to run</param>
/// <param name="args">Arguments</param>
/// <returns>The exit status</returns>
int Run(string exec, string args)
{
    return Run(exec, args, new ProcessSettings());
}

/// <summary>
///  Run the given executable with the given arguments.
/// </summary>
/// <param name="exec">Executable to run</param>
/// <param name="args">Arguments</param>
/// <param name="workingDirectory">Working directory</param>
/// <returns>The exit status</returns>
int Run(string exec, string args, string workingDirectory)
{
    return Run(exec, args, new ProcessSettings { WorkingDirectory = workingDirectory});
}

/// <summary>
///  Run the given executable with the given arguments.
/// </summary>
/// <param name="exec">Executable to run</param>
/// <param name="args">Arguments</param>
/// <param name="workingDirectory">Working directory</param>
/// <returns>The exit status</returns>
int Run(string exec, string args, ProcessSettings settings)
{
    if (settings == null) 
    {
        throw new ArgumentNullException("settings");
    }
    Verbose("{0} {1}", exec, args);
    settings.Arguments = args;
    return StartProcess(exec, settings);
}
