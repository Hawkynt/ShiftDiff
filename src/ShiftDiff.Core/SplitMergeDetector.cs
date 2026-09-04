namespace ShiftDiff.Core;

public static class SplitMergeDetector {
  public static BlockMatch[] Detect(BlockMatch[] matches) {
    var result = (BlockMatch[])matches.Clone();
    var original = (BlockMatch[])matches.Clone();

    for (var index = 0; index < original.Length - 1; index++) {
      var current = original[index];
      var next = original[index + 1];

      if (IsMovedFamily(current.MatchType) && IsMovedFamily(next.MatchType) && current.OldEnd + 1 == next.OldStart) {
        result[index] = result[index] with { MatchType = ChangeType.Split };
        result[index + 1] = result[index + 1] with { MatchType = ChangeType.Split };
      }
    }

    var byNewStart = result
        .Select((match, index) => (Match: match, Index: index))
        .OrderBy(entry => entry.Match.NewStart)
        .ToArray();

    for (var index = 0; index < byNewStart.Length - 1; index++) {
      var current = byNewStart[index];
      var next = byNewStart[index + 1];

      if (IsMovedFamily(current.Match.MatchType) && IsMovedFamily(next.Match.MatchType) && current.Match.NewEnd + 1 == next.Match.NewStart) {
        result[current.Index] = current.Match with { MatchType = ChangeType.Merged };
        result[next.Index] = next.Match with { MatchType = ChangeType.Merged };
      }
    }

    return result;
  }

  private static bool IsMovedFamily(ChangeType type) => type is ChangeType.Moved or ChangeType.MovedEdited;
}
