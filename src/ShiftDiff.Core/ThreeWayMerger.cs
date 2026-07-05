namespace ShiftDiff.Core;

public sealed record MergeResult(string[] Lines, ThreeWayChange[] Conflicts);

public enum ConflictResolution { UseLocal, UseRemote, UseCustom }

public sealed record ConflictResolutionChoice(ConflictResolution Resolution, string? CustomLine = null);

// Resolved output construction (FR-002): auto-resolves every non-conflicting
// ThreeWayChange into a merged line sequence, deferring anything the two
// sides disagree on to the caller instead of guessing. LineDiffer only ever
// emits Unchanged/Removed/Added/Edited (see LineDiffer.Diff), so Conflict is
// the only value ThreeWayComparer itself adds on top of that set.
//
// FR-003's Core-scope kernel (four-way comparison): a caller can supply a
// resolution per conflict (keyed by BaseIndex) so the candidate/resolved
// file has something concrete to be validated against — see FourWayValidator.
public static class ThreeWayMerger
{
    public static MergeResult Merge(ThreeWayChange[] changes) =>
        MergeWithResolutions(changes, new Dictionary<int, ConflictResolutionChoice>());

    public static MergeResult MergeWithResolutions(
        ThreeWayChange[] changes, IReadOnlyDictionary<int, ConflictResolutionChoice> resolutions)
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
                    if (change.BaseIndex.HasValue && resolutions.TryGetValue(change.BaseIndex.Value, out var choice))
                        lines.Add(ResolvedLine(change, choice));
                    else
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

    private static string ResolvedLine(ThreeWayChange change, ConflictResolutionChoice choice) => choice.Resolution switch
    {
        ConflictResolution.UseLocal => change.LocalLine!,
        ConflictResolution.UseRemote => change.RemoteLine!,
        ConflictResolution.UseCustom => choice.CustomLine!,
        _ => throw new ArgumentOutOfRangeException(nameof(choice)),
    };
}
