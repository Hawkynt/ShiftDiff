namespace ShiftDiff.Core;

// A contiguous region of the change list worth showing: every changed line plus
// `contextLines` unchanged lines around it. StartIndex/EndIndex are inclusive
// indices into the LineChange list; OldStart/NewStart are 1-based file line
// numbers (unified-diff convention, 0 when the side contributes no lines).
public sealed record ChangeHunk(int StartIndex, int EndIndex, int OldStart, int OldCount, int NewStart, int NewCount);

// FR-051/UX: lets presenters collapse long unchanged runs instead of rendering
// whole files, and gives the unified/semantic formatters their hunk boundaries.
public static class HunkGrouper {
  public static ChangeHunk[] Group(IReadOnlyList<LineChange> changes, int contextLines = 3) {
    ArgumentNullException.ThrowIfNull(changes);
    if (contextLines < 0) throw new ArgumentOutOfRangeException(nameof(contextLines));

    var spans = new List<(int Start, int End)>();
    for (var i = 0; i < changes.Count; i++) {
      if (changes[i].ChangeType == ChangeType.Unchanged) continue;

      var start = Math.Max(0, i - contextLines);
      var end = Math.Min(changes.Count - 1, i + contextLines);
      if (spans.Count > 0 && start <= spans[^1].End + 1) {
        spans[^1] = (spans[^1].Start, Math.Max(spans[^1].End, end));
        continue;
      }

      spans.Add((start, end));
    }

    var hunks = new ChangeHunk[spans.Count];
    for (var i = 0; i < spans.Count; i++) {
      var (start, end) = spans[i];
      var oldStart = FirstLineNumber(changes, start, end, isOld: true);
      var newStart = FirstLineNumber(changes, start, end, isOld: false);
      var oldCount = Count(changes, start, end, isOld: true);
      var newCount = Count(changes, start, end, isOld: false);
      hunks[i] = new ChangeHunk(start, end, oldStart, oldCount, newStart, newCount);
    }

    return hunks;
  }

  private static int FirstLineNumber(IReadOnlyList<LineChange> changes, int start, int end, bool isOld) {
    for (var i = start; i <= end; i++) {
      var index = isOld ? changes[i].OldIndex : changes[i].NewIndex;
      if (index is { } value) return value + 1;
    }

    return 0;
  }

  private static int Count(IReadOnlyList<LineChange> changes, int start, int end, bool isOld) {
    var count = 0;
    for (var i = start; i <= end; i++) {
      if ((isOld ? changes[i].OldIndex : changes[i].NewIndex) is not null) count++;
    }

    return count;
  }
}
