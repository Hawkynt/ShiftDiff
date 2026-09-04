namespace ShiftDiff.Core;

// Patience-diff-style synchronization: finds lines that occur exactly once in
// both oldKeys and newKeys and returns the longest non-crossing (old-index,
// new-index) sequence of such matches. LineDiffer uses these as safe places
// to split a diff problem into much smaller sub-problems when the exact
// O(n*m) LCS table would be infeasible (FR-050's large-file degraded mode).
public static class UniqueCommonLineSynchronizer {
  public static (int OldIndex, int NewIndex)[] FindAnchors(string[] oldKeys, string[] newKeys, StringComparison comparison) {
    var comparer = comparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    var newCounts = new Dictionary<string, int>(comparer);
    var newIndexByKey = new Dictionary<string, int>(comparer);
    for (var j = 0; j < newKeys.Length; j++) {
      var key = newKeys[j];
      newCounts[key] = newCounts.GetValueOrDefault(key) + 1;
      newIndexByKey[key] = j;
    }

    var oldCounts = new Dictionary<string, int>(comparer);
    foreach (var key in oldKeys) {
      oldCounts[key] = oldCounts.GetValueOrDefault(key) + 1;
    }

    // Built in old-index order, so candidates are already increasing in OldIndex.
    var candidates = new List<(int OldIndex, int NewIndex)>();
    for (var i = 0; i < oldKeys.Length; i++) {
      var key = oldKeys[i];
      if (oldCounts[key] == 1 && newCounts.TryGetValue(key, out var count) && count == 1) {
        candidates.Add((i, newIndexByKey[key]));
      }
    }

    return LongestIncreasingByNewIndex(candidates);
  }

  // Standard patience-sorting LIS (O(n log n)) over candidates' NewIndex — since
  // candidates are already increasing in OldIndex, this selects the longest
  // subsequence that is also increasing in NewIndex, i.e. the longest run of
  // non-crossing matches (the usual patience-diff synchronization step).
  private static (int OldIndex, int NewIndex)[] LongestIncreasingByNewIndex(List<(int OldIndex, int NewIndex)> candidates) {
    if (candidates.Count == 0) {
      return [];
    }

    var tailIndices = new List<int>();
    var predecessors = new int[candidates.Count];

    for (var i = 0; i < candidates.Count; i++) {
      var value = candidates[i].NewIndex;
      var low = 0;
      var high = tailIndices.Count;
      while (low < high) {
        var mid = (low + high) / 2;
        if (candidates[tailIndices[mid]].NewIndex < value) {
          low = mid + 1;
        } else {
          high = mid;
        }
      }

      predecessors[i] = low > 0 ? tailIndices[low - 1] : -1;

      if (low == tailIndices.Count) {
        tailIndices.Add(i);
      } else {
        tailIndices[low] = i;
      }
    }

    var result = new (int OldIndex, int NewIndex)[tailIndices.Count];
    var cursor = tailIndices[^1];
    for (var i = tailIndices.Count - 1; i >= 0; i--) {
      result[i] = candidates[cursor];
      cursor = predecessors[cursor];
    }

    return result;
  }
}
