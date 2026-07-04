namespace ShiftDiff.Core;

public enum ChangeSide { None, Local, Remote, Both }

public sealed record ThreeWayChange(
    ChangeType ChangeType, ChangeSide Side,
    string? BaseLine, string? LocalLine, string? RemoteLine,
    int? BaseIndex = null, int? LocalIndex = null, int? RemoteIndex = null);

// First slice (substitution-only): correlates base<->local and base<->remote
// purely by LineChange.OldIndex, which only lines up 1:1 when neither side
// inserts or deletes lines relative to base. Free insertions (OldIndex ==
// null) are recovered separately below, bucketed by the base index they
// precede — LineDiffer's output is positionally ordered, so this needs no
// re-derivation of position. True diff3 re-alignment for cross-side
// insertion conflicts is still a follow-on slice, not handled here.
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
        var localInserts = GroupInsertionsByAnchor(localChanges, baseLines.Length);
        var remoteInserts = GroupInsertionsByAnchor(remoteChanges, baseLines.Length);
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        var result = new List<ThreeWayChange>();
        void FlushInsertions(int anchorIndex)
        {
            if (localInserts.TryGetValue(anchorIndex, out var localIns))
                foreach (var c in localIns)
                    result.Add(new ThreeWayChange(ChangeType.Added, ChangeSide.Local, null, c.NewLine, null, null, c.NewIndex, null));
            if (remoteInserts.TryGetValue(anchorIndex, out var remoteIns))
                foreach (var c in remoteIns)
                    result.Add(new ThreeWayChange(ChangeType.Added, ChangeSide.Remote, null, null, c.NewLine, null, null, c.NewIndex));
        }

        for (var i = 0; i < baseLines.Length; i++)
        {
            FlushInsertions(i);

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
        FlushInsertions(baseLines.Length);
        return result.ToArray();
    }

    // Buckets free insertions (OldIndex == null) by the base index they
    // precede. A pending insertion belongs to the next base-anchored entry's
    // OldIndex; anything still pending at the end of the array is a trailing
    // insertion, bucketed at baseLineCount.
    private static Dictionary<int, List<LineChange>> GroupInsertionsByAnchor(LineChange[] changes, int baseLineCount)
    {
        var groups = new Dictionary<int, List<LineChange>>();
        var pending = new List<LineChange>();
        foreach (var change in changes)
        {
            if (!change.OldIndex.HasValue) { pending.Add(change); continue; }
            if (pending.Count > 0) { groups[change.OldIndex.Value] = new List<LineChange>(pending); pending.Clear(); }
        }
        if (pending.Count > 0) groups[baseLineCount] = pending;
        return groups;
    }
}
