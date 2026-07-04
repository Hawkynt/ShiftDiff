namespace ShiftDiff.Core;

public sealed record BlockMatch(int OldStart, int OldEnd, int NewStart, int NewEnd, ChangeType MatchType, double Score);
