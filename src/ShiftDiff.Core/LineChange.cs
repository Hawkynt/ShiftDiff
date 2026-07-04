namespace ShiftDiff.Core;

public sealed record LineChange(
    ChangeType ChangeType,
    string? OldLine = null,
    string? NewLine = null,
    int? OldIndex = null,
    int? NewIndex = null);
