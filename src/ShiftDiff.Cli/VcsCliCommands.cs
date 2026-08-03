namespace ShiftDiff.Cli;

// FR-030/FR-031 entry points. Wired to the ShiftDiff.Vcs providers.
public static class VcsCliCommands
{
    public static int Run(CliOptions options, TextWriter output, TextWriter error)
    {
        error.WriteLine($"error: {CliOptionsParser.Name(options.Command)} integration is not available in this build");
        return ExitCode.InvalidInput;
    }
}
