namespace ShiftDiff.Core;

public sealed record TokenChange(
    ChangeType ChangeType,
    string? OldToken = null,
    string? NewToken = null);
