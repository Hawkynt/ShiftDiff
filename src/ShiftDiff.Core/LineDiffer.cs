namespace ShiftDiff.Core;

public static class LineDiffer {
  // Beyond this many DP cells (~80 MB as an int[,] table), the classic
  // O(n*m) LCS table stops being a reasonable in-memory structure — FR-050
  // targets a 100,000-line initial diff, and a full table at that scale
  // would need tens of gigabytes (or simply fail to allocate). Below this
  // threshold DiffMiddleRegion builds the table directly; above it, it
  // degrades via UniqueCommonLineSynchronizer instead of an infeasible
  // allocation (FR-050's degraded-mode tier).
  private const long MaxLcsTableCells = 20_000_000L;

  // Classic LCS-alignment diff: dp[i, j] holds the LCS length of
  // oldLines[i..] and newLines[j..]. Backtracking forward from (0, 0) and
  // preferring the branch with the longer remaining LCS yields the usual
  // "diff" output (minimal Added/Removed set around a common subsequence).
  //
  // Common prefix/suffix lines are trimmed before the DP runs (standard
  // diff-tool optimization, matches git/GNU diffutils) — the DP only ever
  // sees the differing middle region, which is what keeps large-but-mostly-
  // unchanged files (the common real-world case at FR-050's scale) fast and
  // within memory.
  public static LineChange[] Diff(string[] oldLines, string[] newLines, bool ignoreCase = false, WhitespaceMode whitespaceMode = WhitespaceMode.None, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();

    var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    // Precomputed once per side (not per dp-cell) since BuildLcsLengthTable/Backtrack's
    // inner loop is O(n·m) — recomputing the transform per cell would be wasteful.
    var oldKeys = oldLines.Select(line => NormalizeForComparison(line, whitespaceMode)).ToArray();
    var newKeys = newLines.Select(line => NormalizeForComparison(line, whitespaceMode)).ToArray();

    var (prefixLen, suffixLen) = TrimCommonPrefixAndSuffix(oldKeys, newKeys, comparison);
    var oldMidLen = oldKeys.Length - prefixLen - suffixLen;
    var newMidLen = newKeys.Length - prefixLen - suffixLen;

    var oldMidKeys = new ArraySegment<string>(oldKeys, prefixLen, oldMidLen).ToArray();
    var newMidKeys = new ArraySegment<string>(newKeys, prefixLen, newMidLen).ToArray();
    var oldMidLines = new ArraySegment<string>(oldLines, prefixLen, oldMidLen).ToArray();
    var newMidLines = new ArraySegment<string>(newLines, prefixLen, newMidLen).ToArray();

    var rawChanges = new List<LineChange>(oldKeys.Length + newKeys.Length);

    for (var i = 0; i < prefixLen; i++) {
      rawChanges.Add(new LineChange(ChangeType.Unchanged, oldLines[i], newLines[i], OldIndex: i, NewIndex: i));
    }

    rawChanges.AddRange(DiffMiddleRegion(oldMidLines, newMidLines, oldMidKeys, newMidKeys, comparison, oldIndexOffset: prefixLen, newIndexOffset: prefixLen, cancellationToken));

    for (var i = 0; i < suffixLen; i++) {
      var oldIndex = oldKeys.Length - suffixLen + i;
      var newIndex = newKeys.Length - suffixLen + i;
      rawChanges.Add(new LineChange(ChangeType.Unchanged, oldLines[oldIndex], newLines[newIndex], OldIndex: oldIndex, NewIndex: newIndex));
    }

    return CoalesceAdjacentRemovedAndAddedIntoEdited(rawChanges);
  }

  private static string NormalizeForComparison(string line, WhitespaceMode whitespaceMode) => whitespaceMode switch {
    WhitespaceMode.Trim => LineNormalizer.Trim(line),
    // Mirrors LineHasher's WhitespaceNormalized tier: trim first, then collapse internal runs.
    WhitespaceMode.Normalize => LineNormalizer.NormalizeWhitespace(LineNormalizer.Trim(line)),
    // Mirrors LineHasher's TokenNormalized tier: removing all whitespace makes trimming moot.
    WhitespaceMode.RemoveAll => LineNormalizer.RemoveWhitespace(line),
    _ => line,
  };

  // Trims the maximal common prefix and suffix shared by both sides —
  // provably safe for LCS-based diff (any optimal alignment keeps these
  // ends matched), and the standard first step in every production diff
  // tool. Capped so prefix+suffix never exceeds either side's length.
  private static (int prefixLen, int suffixLen) TrimCommonPrefixAndSuffix(string[] oldKeys, string[] newKeys, StringComparison comparison) {
    var minLen = Math.Min(oldKeys.Length, newKeys.Length);

    var prefixLen = 0;
    while (prefixLen < minLen && string.Equals(oldKeys[prefixLen], newKeys[prefixLen], comparison)) {
      prefixLen++;
    }

    var suffixLen = 0;
    while (suffixLen < minLen - prefixLen
        && string.Equals(oldKeys[oldKeys.Length - 1 - suffixLen], newKeys[newKeys.Length - 1 - suffixLen], comparison)) {
      suffixLen++;
    }

    return (prefixLen, suffixLen);
  }

  // Diffs one contiguous (already prefix/suffix-trimmed) region. Below the DP
  // cap this builds the exact LCS table directly; above it, degrades via
  // UniqueCommonLineSynchronizer instead of an infeasible allocation
  // (FR-050) — each recursive call operates on a strictly smaller region, so
  // this always terminates in a bounded-memory result.
  private static List<LineChange> DiffMiddleRegion(string[] oldLines, string[] newLines, string[] oldKeys, string[] newKeys, StringComparison comparison, int oldIndexOffset, int newIndexOffset, CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();

    var cellCount = (long)(oldKeys.Length + 1) * (newKeys.Length + 1);
    if (cellCount <= MaxLcsTableCells) {
      var dp = BuildLcsLengthTable(oldKeys, newKeys, comparison, cancellationToken);
      return Backtrack(oldLines, newLines, oldKeys, newKeys, dp, comparison, oldIndexOffset, newIndexOffset);
    }

    var anchors = UniqueCommonLineSynchronizer.FindAnchors(oldKeys, newKeys, comparison);
    if (anchors.Length == 0) {
      // No shared unique line anywhere in this region to synchronize on —
      // the coarsest valid result (whole region replaced) instead of an
      // O(n*m) table that would not fit in memory.
      return ReplaceWholeRegion(oldLines, newLines, oldIndexOffset, newIndexOffset);
    }

    var result = new List<LineChange>(oldLines.Length + newLines.Length);
    var oldStart = 0;
    var newStart = 0;

    foreach (var (oldAnchor, newAnchor) in anchors) {
      cancellationToken.ThrowIfCancellationRequested();

      result.AddRange(DiffMiddleRegion(
          oldLines[oldStart..oldAnchor], newLines[newStart..newAnchor],
          oldKeys[oldStart..oldAnchor], newKeys[newStart..newAnchor],
          comparison, oldIndexOffset + oldStart, newIndexOffset + newStart, cancellationToken));

      result.Add(new LineChange(ChangeType.Unchanged, oldLines[oldAnchor], newLines[newAnchor],
          OldIndex: oldIndexOffset + oldAnchor, NewIndex: newIndexOffset + newAnchor));

      oldStart = oldAnchor + 1;
      newStart = newAnchor + 1;
    }

    result.AddRange(DiffMiddleRegion(
        oldLines[oldStart..], newLines[newStart..],
        oldKeys[oldStart..], newKeys[newStart..],
        comparison, oldIndexOffset + oldStart, newIndexOffset + newStart, cancellationToken));

    return result;
  }

  private static List<LineChange> ReplaceWholeRegion(string[] oldLines, string[] newLines, int oldIndexOffset, int newIndexOffset) {
    var result = new List<LineChange>(oldLines.Length + newLines.Length);

    for (var i = 0; i < oldLines.Length; i++) {
      result.Add(new LineChange(ChangeType.Removed, oldLines[i], null, OldIndex: oldIndexOffset + i));
    }

    for (var j = 0; j < newLines.Length; j++) {
      result.Add(new LineChange(ChangeType.Added, null, newLines[j], NewIndex: newIndexOffset + j));
    }

    return result;
  }

  private static int[,] BuildLcsLengthTable(string[] oldKeys, string[] newKeys, StringComparison comparison, CancellationToken cancellationToken) {
    var dp = new int[oldKeys.Length + 1, newKeys.Length + 1];

    for (var i = oldKeys.Length - 1; i >= 0; i--) {
      // Checked once per row rather than per cell — each row is already
      // O(newKeys.Length) work, so the extra check is negligible overhead
      // while still making a 100,000-line diff (FR-050's own target)
      // interruptible within one row's worth of latency.
      cancellationToken.ThrowIfCancellationRequested();

      for (var j = newKeys.Length - 1; j >= 0; j--) {
        dp[i, j] = string.Equals(oldKeys[i], newKeys[j], comparison)
            ? dp[i + 1, j + 1] + 1
            : Math.Max(dp[i + 1, j], dp[i, j + 1]);
      }
    }

    return dp;
  }

  private static List<LineChange> Backtrack(string[] oldLines, string[] newLines, string[] oldKeys, string[] newKeys, int[,] dp, StringComparison comparison, int oldIndexOffset, int newIndexOffset) {
    var result = new List<LineChange>();
    var i = 0;
    var j = 0;

    while (i < oldLines.Length && j < newLines.Length) {
      if (string.Equals(oldKeys[i], newKeys[j], comparison)) {
        result.Add(new LineChange(ChangeType.Unchanged, oldLines[i], newLines[j], OldIndex: i + oldIndexOffset, NewIndex: j + newIndexOffset));
        i++;
        j++;
      } else if (dp[i + 1, j] >= dp[i, j + 1]) {
        result.Add(new LineChange(ChangeType.Removed, oldLines[i], null, OldIndex: i + oldIndexOffset));
        i++;
      } else {
        result.Add(new LineChange(ChangeType.Added, null, newLines[j], NewIndex: j + newIndexOffset));
        j++;
      }
    }

    while (i < oldLines.Length) {
      result.Add(new LineChange(ChangeType.Removed, oldLines[i], null, OldIndex: i + oldIndexOffset));
      i++;
    }

    while (j < newLines.Length) {
      result.Add(new LineChange(ChangeType.Added, null, newLines[j], NewIndex: j + newIndexOffset));
      j++;
    }

    return result;
  }

  // A Removed run immediately followed by an Added run reads as a
  // substitution, not an unrelated delete+insert — pair them positionally
  // into Edited entries (git's "replace" hunk semantics), leaving any
  // count mismatch as leftover Removed/Added.
  private static LineChange[] CoalesceAdjacentRemovedAndAddedIntoEdited(List<LineChange> changes) {
    var result = new List<LineChange>(changes.Count);
    var index = 0;

    while (index < changes.Count) {
      if (changes[index].ChangeType != ChangeType.Removed) {
        result.Add(changes[index]);
        index++;
        continue;
      }

      var removedStart = index;
      while (index < changes.Count && changes[index].ChangeType == ChangeType.Removed) {
        index++;
      }

      var addedStart = index;
      while (index < changes.Count && changes[index].ChangeType == ChangeType.Added) {
        index++;
      }

      var removedCount = addedStart - removedStart;
      var addedCount = index - addedStart;
      var pairCount = Math.Min(removedCount, addedCount);

      for (var pair = 0; pair < pairCount; pair++) {
        result.Add(new LineChange(
            ChangeType.Edited,
            changes[removedStart + pair].OldLine,
            changes[addedStart + pair].NewLine,
            OldIndex: changes[removedStart + pair].OldIndex,
            NewIndex: changes[addedStart + pair].NewIndex));
      }

      for (var leftover = pairCount; leftover < removedCount; leftover++) {
        result.Add(changes[removedStart + leftover]);
      }

      for (var leftover = pairCount; leftover < addedCount; leftover++) {
        result.Add(changes[addedStart + leftover]);
      }
    }

    return result.ToArray();
  }
}
