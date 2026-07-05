namespace ShiftDiff.Core;

public sealed record ValidationResult(bool Matches, LineChange[] Discrepancies);

// FR-003's Core-scope kernel: "detect whether the fourth file correctly
// incorporates selected changes" — build the expected lines from
// ThreeWayMerger.MergeWithResolutions, then diff them against the
// candidate/resolved file. Any non-Unchanged entry is a place the
// candidate deviated from what was actually selected.
public static class FourWayValidator
{
    public static ValidationResult Validate(string[] expectedLines, string[] candidateLines)
    {
        var diff = LineDiffer.Diff(expectedLines, candidateLines);
        var discrepancies = diff.Where(c => c.ChangeType != ChangeType.Unchanged).ToArray();
        return new ValidationResult(discrepancies.Length == 0, discrepancies);
    }
}
