namespace ShiftDiff.Core;

public static class LineDiffer
{
    // Classic LCS-alignment diff: dp[i, j] holds the LCS length of
    // oldLines[i..] and newLines[j..]. Backtracking forward from (0, 0) and
    // preferring the branch with the longer remaining LCS yields the usual
    // "diff" output (minimal Added/Removed set around a common subsequence).
    public static LineChange[] Diff(string[] oldLines, string[] newLines, bool ignoreCase = false, WhitespaceMode whitespaceMode = WhitespaceMode.None)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        // Precomputed once per side (not per dp-cell) since BuildLcsLengthTable/Backtrack's
        // inner loop is O(n·m) — recomputing the transform per cell would be wasteful.
        var oldKeys = oldLines.Select(line => NormalizeForComparison(line, whitespaceMode)).ToArray();
        var newKeys = newLines.Select(line => NormalizeForComparison(line, whitespaceMode)).ToArray();
        var dp = BuildLcsLengthTable(oldKeys, newKeys, comparison);
        var rawChanges = Backtrack(oldLines, newLines, oldKeys, newKeys, dp, comparison);
        return CoalesceAdjacentRemovedAndAddedIntoEdited(rawChanges);
    }

    private static string NormalizeForComparison(string line, WhitespaceMode whitespaceMode) => whitespaceMode switch
    {
        WhitespaceMode.Trim => LineNormalizer.Trim(line),
        // Mirrors LineHasher's WhitespaceNormalized tier: trim first, then collapse internal runs.
        WhitespaceMode.Normalize => LineNormalizer.NormalizeWhitespace(LineNormalizer.Trim(line)),
        // Mirrors LineHasher's TokenNormalized tier: removing all whitespace makes trimming moot.
        WhitespaceMode.RemoveAll => LineNormalizer.RemoveWhitespace(line),
        _ => line,
    };

    private static int[,] BuildLcsLengthTable(string[] oldKeys, string[] newKeys, StringComparison comparison)
    {
        var dp = new int[oldKeys.Length + 1, newKeys.Length + 1];

        for (var i = oldKeys.Length - 1; i >= 0; i--)
        {
            for (var j = newKeys.Length - 1; j >= 0; j--)
            {
                dp[i, j] = string.Equals(oldKeys[i], newKeys[j], comparison)
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        return dp;
    }

    private static List<LineChange> Backtrack(string[] oldLines, string[] newLines, string[] oldKeys, string[] newKeys, int[,] dp, StringComparison comparison)
    {
        var result = new List<LineChange>();
        var i = 0;
        var j = 0;

        while (i < oldLines.Length && j < newLines.Length)
        {
            if (string.Equals(oldKeys[i], newKeys[j], comparison))
            {
                result.Add(new LineChange(ChangeType.Unchanged, oldLines[i], newLines[j], OldIndex: i, NewIndex: j));
                i++;
                j++;
            }
            else if (dp[i + 1, j] >= dp[i, j + 1])
            {
                result.Add(new LineChange(ChangeType.Removed, oldLines[i], null, OldIndex: i));
                i++;
            }
            else
            {
                result.Add(new LineChange(ChangeType.Added, null, newLines[j], NewIndex: j));
                j++;
            }
        }

        while (i < oldLines.Length)
        {
            result.Add(new LineChange(ChangeType.Removed, oldLines[i], null, OldIndex: i));
            i++;
        }

        while (j < newLines.Length)
        {
            result.Add(new LineChange(ChangeType.Added, null, newLines[j], NewIndex: j));
            j++;
        }

        return result;
    }

    // A Removed run immediately followed by an Added run reads as a
    // substitution, not an unrelated delete+insert — pair them positionally
    // into Edited entries (git's "replace" hunk semantics), leaving any
    // count mismatch as leftover Removed/Added.
    private static LineChange[] CoalesceAdjacentRemovedAndAddedIntoEdited(List<LineChange> changes)
    {
        var result = new List<LineChange>(changes.Count);
        var index = 0;

        while (index < changes.Count)
        {
            if (changes[index].ChangeType != ChangeType.Removed)
            {
                result.Add(changes[index]);
                index++;
                continue;
            }

            var removedStart = index;
            while (index < changes.Count && changes[index].ChangeType == ChangeType.Removed)
            {
                index++;
            }

            var addedStart = index;
            while (index < changes.Count && changes[index].ChangeType == ChangeType.Added)
            {
                index++;
            }

            var removedCount = addedStart - removedStart;
            var addedCount = index - addedStart;
            var pairCount = Math.Min(removedCount, addedCount);

            for (var pair = 0; pair < pairCount; pair++)
            {
                result.Add(new LineChange(
                    ChangeType.Edited,
                    changes[removedStart + pair].OldLine,
                    changes[addedStart + pair].NewLine,
                    OldIndex: changes[removedStart + pair].OldIndex,
                    NewIndex: changes[addedStart + pair].NewIndex));
            }

            for (var leftover = pairCount; leftover < removedCount; leftover++)
            {
                result.Add(changes[removedStart + leftover]);
            }

            for (var leftover = pairCount; leftover < addedCount; leftover++)
            {
                result.Add(changes[addedStart + leftover]);
            }
        }

        return result.ToArray();
    }
}
