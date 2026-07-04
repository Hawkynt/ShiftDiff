namespace ShiftDiff.Core;

public enum ChangeSide { None, Local, Remote, Both }

public sealed record ThreeWayChange(
    ChangeType ChangeType, ChangeSide Side,
    string? BaseLine, string? LocalLine, string? RemoteLine,
    int? BaseIndex = null, int? LocalIndex = null, int? RemoteIndex = null);

// First slice (substitution-only): correlates base<->local and base<->remote
// purely by LineChange.OldIndex, which only lines up 1:1 when neither side
// inserts or deletes lines relative to base. True diff3 re-alignment for
// insertions/deletions is a follow-on slice, not handled here.
public static class ThreeWayComparer
{
    public static ThreeWayChange[] Compare(
        string[] baseLines, string[] localLines, string[] remoteLines,
        bool ignoreCase = false, WhitespaceMode whitespaceMode = WhitespaceMode.None)
    {
        var localChanges = LineDiffer.Diff(baseLines, localLines, ignoreCase, whitespaceMode);
        var remoteChanges = LineDiffer.Diff(baseLines, remoteLines, ignoreCase, whitespaceMode);
        var localByBase = localChanges.Where(c => c.OldIndex.HasValue).ToDictionary(c => c.OldIndex!.Value);
        var remoteByBase = remoteChanges.Where(c => c.OldIndex.HasValue).ToDictionary(c => c.OldIndex!.Value);
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        var result = new List<ThreeWayChange>();
        for (var i = 0; i < baseLines.Length; i++)
        {
            var local = localByBase[i];
            var remote = remoteByBase[i];
            var localChanged = local.ChangeType != ChangeType.Unchanged;
            var remoteChanged = remote.ChangeType != ChangeType.Unchanged;

            var (type, side) = (localChanged, remoteChanged) switch
            {
                (false, false) => (ChangeType.Unchanged, ChangeSide.None),
                (true, false) => (local.ChangeType, ChangeSide.Local),
                (false, true) => (remote.ChangeType, ChangeSide.Remote),
                (true, true) when string.Equals(local.NewLine, remote.NewLine, comparison)
                    => (local.ChangeType, ChangeSide.Both),
                _ => (ChangeType.Conflict, ChangeSide.Both),
            };

            result.Add(new ThreeWayChange(type, side, baseLines[i], local.NewLine, remote.NewLine, i, local.NewIndex, remote.NewIndex));
        }
        return result.ToArray();
    }
}
