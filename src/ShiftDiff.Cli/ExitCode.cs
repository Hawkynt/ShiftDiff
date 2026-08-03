namespace ShiftDiff.Cli;

// SPEC section 12: exit codes for CI usage.
public static class ExitCode
{
    public const int NoDifferences = 0;
    public const int DifferencesFound = 1;
    public const int Conflicts = 2;
    public const int InvalidInput = 3;
    public const int InternalError = 4;
}
