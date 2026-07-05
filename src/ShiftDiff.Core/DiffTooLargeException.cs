namespace ShiftDiff.Core;

// Thrown by LineDiffer.Diff when the differing middle region (after common
// prefix/suffix trimming) is still too large for the O(n*m) LCS table —
// FR-050's "1,000,000 lines degraded mode" case. A caller hitting this needs
// a different strategy (line-count limit, chunked/progressive comparison),
// not a silent OutOfMemoryException.
public sealed class DiffTooLargeException : Exception
{
    public int OldLineCount { get; }
    public int NewLineCount { get; }
    public int TrimmedOldLineCount { get; }
    public int TrimmedNewLineCount { get; }

    public DiffTooLargeException(int oldLineCount, int newLineCount, int trimmedOldLineCount, int trimmedNewLineCount, long maxCells)
        : base($"Cannot diff {oldLineCount:N0} vs {newLineCount:N0} lines: after common prefix/suffix trimming, " +
               $"the differing region is still {trimmedOldLineCount:N0}x{trimmedNewLineCount:N0} lines, exceeding " +
               $"the {maxCells:N0}-cell LCS table limit. This file pair needs a chunked or degraded-mode comparison strategy.")
    {
        OldLineCount = oldLineCount;
        NewLineCount = newLineCount;
        TrimmedOldLineCount = trimmedOldLineCount;
        TrimmedNewLineCount = trimmedNewLineCount;
    }
}
