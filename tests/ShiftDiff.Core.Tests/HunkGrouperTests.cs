using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

public class HunkGrouperTests {
  [Fact]
  public void Group_WithNoChanges_ReturnsNoHunks() {
    var changes = Unchanged(10);

    Assert.Empty(HunkGrouper.Group(changes));
  }

  [Fact]
  public void Group_WithEmptyChangeList_ReturnsNoHunks() {
    Assert.Empty(HunkGrouper.Group([]));
  }

  [Fact]
  public void Group_SingleChangeInMiddle_SurroundsItWithContext() {
    var changes = Unchanged(20);
    changes[10] = new LineChange(ChangeType.Edited, "old", "new", 10, 10);

    var hunks = HunkGrouper.Group(changes, contextLines: 3);

    var hunk = Assert.Single(hunks);
    Assert.Equal(7, hunk.StartIndex);
    Assert.Equal(13, hunk.EndIndex);
    Assert.Equal(8, hunk.OldStart);
    Assert.Equal(7, hunk.OldCount);
    Assert.Equal(8, hunk.NewStart);
    Assert.Equal(7, hunk.NewCount);
  }

  [Fact]
  public void Group_ChangeAtFileStart_ClampsContextToFirstLine() {
    var changes = Unchanged(10);
    changes[0] = new LineChange(ChangeType.Added, null, "new", null, 0);

    var hunk = Assert.Single(HunkGrouper.Group(changes, contextLines: 3));

    Assert.Equal(0, hunk.StartIndex);
    Assert.Equal(3, hunk.EndIndex);
  }

  [Fact]
  public void Group_ChangeAtFileEnd_ClampsContextToLastLine() {
    var changes = Unchanged(10);
    changes[9] = new LineChange(ChangeType.Removed, "old", null, 9, null);

    var hunk = Assert.Single(HunkGrouper.Group(changes, contextLines: 3));

    Assert.Equal(6, hunk.StartIndex);
    Assert.Equal(9, hunk.EndIndex);
  }

  [Fact]
  public void Group_TwoChangesFarApart_ProducesTwoHunks() {
    var changes = Unchanged(40);
    changes[5] = new LineChange(ChangeType.Edited, "a", "b", 5, 5);
    changes[30] = new LineChange(ChangeType.Edited, "c", "d", 30, 30);

    var hunks = HunkGrouper.Group(changes, contextLines: 3);

    Assert.Equal(2, hunks.Length);
    Assert.Equal(2, hunks[0].StartIndex);
    Assert.Equal(27, hunks[1].StartIndex);
  }

  [Fact]
  public void Group_TwoChangesWithinDoubleContext_MergesIntoOneHunk() {
    var changes = Unchanged(40);
    changes[5] = new LineChange(ChangeType.Edited, "a", "b", 5, 5);
    changes[11] = new LineChange(ChangeType.Edited, "c", "d", 11, 11);

    var hunk = Assert.Single(HunkGrouper.Group(changes, contextLines: 3));

    Assert.Equal(2, hunk.StartIndex);
    Assert.Equal(14, hunk.EndIndex);
  }

  [Fact]
  public void Group_AddedOnlyHunk_ReportsZeroOldStartWhenNoOldLinePresent() {
    var changes = new List<LineChange> { new(ChangeType.Added, null, "new", null, 0) };

    var hunk = Assert.Single(HunkGrouper.Group(changes, contextLines: 0));

    Assert.Equal(0, hunk.OldStart);
    Assert.Equal(0, hunk.OldCount);
    Assert.Equal(1, hunk.NewStart);
    Assert.Equal(1, hunk.NewCount);
  }

  [Fact]
  public void Group_WithZeroContext_ReturnsOnlyChangedLines() {
    var changes = Unchanged(10);
    changes[4] = new LineChange(ChangeType.Edited, "a", "b", 4, 4);

    var hunk = Assert.Single(HunkGrouper.Group(changes, contextLines: 0));

    Assert.Equal(4, hunk.StartIndex);
    Assert.Equal(4, hunk.EndIndex);
  }

  [Fact]
  public void Group_WithNegativeContext_Throws() {
    Assert.Throws<ArgumentOutOfRangeException>(() => HunkGrouper.Group([], -1));
  }

  [Fact]
  public void Group_WithZeroContext_KeepsGappedChangesInSeparateHunks() {
    var changes = Unchanged(20);
    changes[5] = new LineChange(ChangeType.Edited, "a", "b", 5, 5);
    changes[7] = new LineChange(ChangeType.Edited, "c", "d", 7, 7);

    var hunks = HunkGrouper.Group(changes, contextLines: 0);

    Assert.Equal(2, hunks.Length);
  }

  [Fact]
  public void Group_ConsecutiveChangedLines_FormOneHunk() {
    var changes = Unchanged(20);
    changes[5] = new LineChange(ChangeType.Removed, "a", null, 5, null);
    changes[6] = new LineChange(ChangeType.Added, null, "b", null, 5);

    var hunk = Assert.Single(HunkGrouper.Group(changes, contextLines: 0));

    Assert.Equal(5, hunk.StartIndex);
    Assert.Equal(6, hunk.EndIndex);
  }

  private static List<LineChange> Unchanged(int count) =>
      Enumerable.Range(0, count)
          .Select(i => new LineChange(ChangeType.Unchanged, $"line{i}", $"line{i}", i, i))
          .ToList();
}
