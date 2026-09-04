using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

public class DiffFilterAndOverviewTests {
  [Fact]
  public void Apply_WithoutSearchOrFilter_ReturnsTheSameRows() {
    var rows = Rows();

    Assert.Same(rows, DiffFilter.Apply(rows));
  }

  [Fact]
  public void Apply_SearchText_KeepsOnlyRowsContainingIt() {
    var filtered = DiffFilter.Apply(Rows(), "beta");

    var row = Assert.Single(filtered);
    Assert.Contains("beta", row.Cells[1].Text);
  }

  [Fact]
  public void Apply_SearchText_IsCaseInsensitive() {
    Assert.Single(DiffFilter.Apply(Rows(), "BETA"));
  }

  [Fact]
  public void Apply_OnlyChangesFilter_HidesUnchangedRows() {
    var filtered = DiffFilter.Apply(Rows(), null, ChangeTypeFilter.OnlyChanges);

    Assert.All(filtered, row => Assert.True(row.IsChanged));
  }

  [Fact]
  public void Apply_SingleTypeFilter_KeepsOnlyThatType() {
    var filtered = DiffFilter.Apply(Rows(), null, ChangeTypeFilter.Added);

    Assert.All(filtered, row => Assert.Equal(ChangeType.Added, row.ChangeType));
  }

  [Fact]
  public void Apply_MovedFilter_MatchesRowsFlaggedAsMoved() {
    var rows = new List<DiffRow>
    {
            Row(ChangeType.Unchanged, "kept") with { IsMoved = true },
            Row(ChangeType.Unchanged, "plain"),
        };

    var filtered = DiffFilter.Apply(rows, null, ChangeTypeFilter.Moved);

    Assert.Single(filtered);
  }

  [Fact]
  public void FindMatches_ReturnsEveryMatchingRowIndex() {
    var indices = DiffFilter.FindMatches(Rows(), "a");

    Assert.NotEmpty(indices);
    Assert.All(indices, index => Assert.InRange(index, 0, 3));
  }

  [Fact]
  public void FindMatches_EmptySearch_ReturnsNothing() {
    Assert.Empty(DiffFilter.FindMatches(Rows(), string.Empty));
  }

  [Fact]
  public void Build_Overview_ProducesOneStripePerChangeRun() {
    var rows = new List<DiffRow>
    {
            Row(ChangeType.Unchanged, "a"),
            Row(ChangeType.Added, "b"),
            Row(ChangeType.Added, "c"),
            Row(ChangeType.Unchanged, "d"),
            Row(ChangeType.Removed, "e"),
        };

    var stripes = OverviewBuilder.Build(rows);

    Assert.Equal(2, stripes.Count);
    Assert.Equal(ChangeType.Added, stripes[0].ChangeType);
    Assert.Equal(ChangeType.Removed, stripes[1].ChangeType);
  }

  [Fact]
  public void Build_Overview_PositionsStripesInNormalizedDocumentSpace() {
    var rows = Enumerable.Range(0, 10).Select(i => Row(i == 5 ? ChangeType.Added : ChangeType.Unchanged, $"l{i}")).ToList();

    var stripe = Assert.Single(OverviewBuilder.Build(rows));

    Assert.Equal(0.5, stripe.Start, 3);
    Assert.Equal(0.6, stripe.End, 3);
    Assert.Equal(5, stripe.RowIndex);
  }

  [Fact]
  public void Build_Overview_OfAnEmptyDocument_ProducesNoStripes() {
    Assert.Empty(OverviewBuilder.Build([]));
  }

  [Fact]
  public void Build_Overview_AdjacentRunsOfDifferentKinds_StaySeparate() {
    var rows = new List<DiffRow> { Row(ChangeType.Added, "a"), Row(ChangeType.Removed, "b") };

    Assert.Equal(2, OverviewBuilder.Build(rows).Count);
  }

  private static List<DiffRow> Rows() =>
  [
      Row(ChangeType.Unchanged, "alpha"),
        Row(ChangeType.Added, "beta"),
        Row(ChangeType.Removed, "gamma"),
        Row(ChangeType.Edited, "delta"),
    ];

  private static DiffRow Row(ChangeType type, string text) {
    var cell = new DiffCell(1, [new DiffSegment(text, DiffSegmentKind.Unchanged)], CellState.Unchanged);
    return new DiffRow(DiffRowKind.Line, type, [cell, cell]);
  }
}
