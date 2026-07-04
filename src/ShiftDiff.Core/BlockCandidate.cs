namespace ShiftDiff.Core;

public sealed record BlockCandidate(int OldStart, int OldEnd, int NewStart, int NewEnd);
