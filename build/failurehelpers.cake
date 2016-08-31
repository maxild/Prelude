// should be ext method....not possible until cake is running on CoreCLR with up to date Roslyn bits
public class FailureHelper
{
    public static void ExceptionOnError(int exitCode, string errorMsg)
    {
        if (exitCode != 0)
        {
            throw new System.Exception(errorMsg);
        }
    }
}

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
