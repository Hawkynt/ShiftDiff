namespace ShiftDiff.Core;

public sealed record MergeResult(string[] Lines, ThreeWayChange[] Conflicts);

// Resolved output construction (FR-002): auto-resolves every non-conflicting
// ThreeWayChange into a merged line sequence, deferring anything the two
// sides disagree on to the caller instead of guessing. LineDiffer only ever
// emits Unchanged/Removed/Added/Edited (see LineDiffer.Diff), so Conflict is
// the only value ThreeWayComparer itself adds on top of that set.
public static class ThreeWayMerger
{
    public static MergeResult Merge(ThreeWayChange[] changes)
    {
        var lines = new List<string>();
        var conflicts = new List<ThreeWayChange>();

        foreach (var change in changes)
        {
            switch (change.ChangeType)
            {
                case ChangeType.Removed:
                    break;
                case ChangeType.Conflict:
                    conflicts.Add(change);
                    break;
                case ChangeType.Unchanged:
                    lines.Add(change.BaseLine!);
                    break;
                default:
                    lines.Add(change.LocalLine ?? change.RemoteLine!);
                    break;
            }
        }

        return new MergeResult(lines.ToArray(), conflicts.ToArray());
    }
}
