using System.Text;
using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

public class DiffDocumentBuilderTests {
  private readonly ComparisonSettings _settings = new() { CollapseUnchanged = false };

  [Fact]
  public void BuildTwoWay_IdenticalFiles_ProducesOnlyUnchangedRows() {
    var document = Build("one\ntwo\n", "one\ntwo\n");

    Assert.Equal(2, document.Rows.Count);
    Assert.All(document.Rows, row => Assert.False(row.IsChanged));
    Assert.False(document.Summary.HasDifferences);
    Assert.Empty(document.ChangeRowIndices);
  }

  [Fact]
  public void BuildTwoWay_AddedLine_LeavesTheLeftPaneEmptyForThatRow() {
    var document = Build("one\n", "one\ntwo\n");

    var added = Assert.Single(document.Rows, row => row.ChangeType == ChangeType.Added);
    Assert.Equal(CellState.Empty, added.Cells[0].State);
    Assert.Equal(CellState.Added, added.Cells[1].State);
    Assert.Equal("two", added.Cells[1].Text);
    Assert.Equal(2, added.Cells[1].LineNumber);
  }

  [Fact]
  public void BuildTwoWay_RemovedLine_LeavesTheRightPaneEmptyForThatRow() {
    var document = Build("one\ntwo\n", "one\n");

    var removed = Assert.Single(document.Rows, row => row.ChangeType == ChangeType.Removed);
    Assert.Equal(CellState.Removed, removed.Cells[0].State);
    Assert.Equal(CellState.Empty, removed.Cells[1].State);
  }

  [Fact]
  public void BuildTwoWay_EditedLine_CarriesTokenLevelSegmentsOnBothSides() {
    var document = Build("value = 1\n", "value = 2\n");

    var edited = Assert.Single(document.Rows, row => row.ChangeType == ChangeType.Edited);
    Assert.Contains(edited.Cells[0].Segments, segment => segment.Kind == DiffSegmentKind.Removed);
    Assert.Contains(edited.Cells[1].Segments, segment => segment.Kind == DiffSegmentKind.Added);
  }

  [Fact]
  public void BuildTwoWay_LineNumbers_AreOneBasedPerSide() {
    var document = Build("a\nb\n", "a\nb\n");

    Assert.Equal(1, document.Rows[0].Cells[0].LineNumber);
    Assert.Equal(2, document.Rows[1].Cells[1].LineNumber);
  }

  [Fact]
  public void BuildTwoWay_Summary_CountsEachChangeKind() {
    var document = Build("keep\ndrop\nedit\n", "keep\nedit-ed\nadd\n");

    Assert.True(document.Summary.HasDifferences);
    Assert.Equal(document.Rows.Count(row => row.ChangeType == ChangeType.Added), document.Summary.Added);
    Assert.Equal(document.Rows.Count(row => row.ChangeType == ChangeType.Removed), document.Summary.Removed);
    Assert.Equal(document.Rows.Count(row => row.ChangeType == ChangeType.Edited), document.Summary.Edited);
  }

  [Fact]
  public void BuildTwoWay_ChangeRowIndices_PointAtTheFirstRowOfEachChangeRun() {
    var document = Build("a\nb\nc\nd\ne\n", "a\nB\nC\nd\nE\n");

    Assert.Equal(2, document.ChangeRowIndices.Count);
  }

  [Fact]
  public void BuildTwoWay_ReorderedMethod_ReportsAMovedBlockWithBothRowEnds() {
    var document = Build(ReorderedOld, ReorderedNew, new ComparisonSettings {
      CollapseUnchanged = false,
      Detection = DetectionMode.Aggressive,
    });

    var block = Assert.Single(document.MovedBlocks);
    Assert.True(block.OldRowIndex >= 0);
    Assert.True(block.NewRowIndex >= 0);
    Assert.Equal(1, document.Summary.MovedBlocks);
  }

  [Fact]
  public void BuildTwoWay_WithCollapsing_FoldsDistantUnchangedRuns() {
    var oldText = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"line {i}"));
    var newText = oldText.Replace("line 20", "line twenty");
    var settings = new ComparisonSettings { CollapseUnchanged = true, ContextLines = 3 };

    var document = Build(oldText, newText, settings);

    Assert.Contains(document.Rows, row => row.Kind == DiffRowKind.Collapsed);
    Assert.True(document.Rows.Count < 40);
    Assert.Contains(document.Rows, row => row.HiddenLineCount > 1);
  }

  [Fact]
  public void BuildTwoWay_WithCollapsing_KeepsContextRowsAroundEachChange() {
    var oldText = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"line {i}"));
    var newText = oldText.Replace("line 20", "line twenty");

    var document = Build(oldText, newText, new ComparisonSettings { CollapseUnchanged = true, ContextLines = 2 });

    var changeIndex = document.Rows.ToList().FindIndex(row => row.IsChanged);
    Assert.Equal(DiffRowKind.Line, document.Rows[changeIndex - 1].Kind);
    Assert.Equal(DiffRowKind.Line, document.Rows[changeIndex - 2].Kind);
  }

  [Fact]
  public void BuildTwoWay_SyntaxHighlightingDisabled_ProducesNoSyntaxSplits() {
    var settings = new ComparisonSettings { CollapseUnchanged = false, SyntaxHighlighting = false };

    var document = Build("if (x) { return 1; }\n", "if (x) { return 1; }\n", settings, ".cs");

    Assert.Single(document.Rows[0].Cells[0].Segments);
  }

  [Fact]
  public void ToUnified_EditedRow_BecomesARemovedRowFollowedByAnAddedRow() {
    var unified = DiffDocumentBuilder.ToUnified(Build("value = 1\n", "value = 2\n"));

    Assert.Single(unified.PaneTitles);
    Assert.Contains(unified.Rows, row => row.ChangeType == ChangeType.Removed);
    Assert.Contains(unified.Rows, row => row.ChangeType == ChangeType.Added);
    Assert.All(unified.Rows, row => Assert.Single(row.Cells));
  }

  [Fact]
  public void BuildThreeWay_ConflictingSides_MarksBothPanesAsConflicting() {
    var changes = ThreeWayComparer.Compare(["one", "two"], ["one", "TWO-local"], ["one", "two-remote"]);

    var document = DiffDocumentBuilder.BuildThreeWay(changes, _settings);

    Assert.Equal(3, document.PaneCount);
    Assert.Equal(1, document.Summary.Conflicts);
    var conflict = Assert.Single(document.Rows, row => row.IsConflict);
    Assert.Equal(CellState.Conflict, conflict.Cells[1].State);
    Assert.Equal(CellState.Conflict, conflict.Cells[2].State);
    Assert.Single(document.ConflictRowIndices);
  }

  [Fact]
  public void BuildThreeWay_LocalOnlyEdit_LeavesTheRemotePaneUnmarked() {
    var changes = ThreeWayComparer.Compare(["one", "two"], ["one", "TWO"], ["one", "two"]);

    var document = DiffDocumentBuilder.BuildThreeWay(changes, _settings);

    var edited = Assert.Single(document.Rows, row => row.ChangeType != ChangeType.Unchanged);
    Assert.Equal(CellState.Unchanged, edited.Cells[2].State);
  }

  [Fact]
  public void BuildFourWay_TargetMatchingTheMerge_ShowsAnUnchangedFourthPane() {
    var changes = ThreeWayComparer.Compare(["one", "two"], ["one", "TWO"], ["one", "two"]);

    var document = DiffDocumentBuilder.BuildFourWay(changes, ["one", "TWO"], _settings);

    Assert.Equal(4, document.PaneCount);
    Assert.All(document.Rows, row => Assert.NotEqual(CellState.Edited, row.Cells[3].State));
  }

  [Fact]
  public void BuildFourWay_TargetDivergingFromTheMerge_MarksTheFourthPane() {
    var changes = ThreeWayComparer.Compare(["one", "two"], ["one", "TWO"], ["one", "two"]);

    var document = DiffDocumentBuilder.BuildFourWay(changes, ["one", "something-else"], _settings);

    Assert.Contains(document.Rows, row => row.Cells[3].State == CellState.Edited);
  }

  private DiffDocument Build(string oldText, string newText, ComparisonSettings? settings = null, string extension = ".txt") {
    var result = FileComparer.CompareSourceFiles(
        Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText),
        "old" + extension, "new" + extension,
        (settings ?? _settings).IgnoreCase, (settings ?? _settings).Whitespace, (settings ?? _settings).Detection);

    return DiffDocumentBuilder.BuildTwoWay(result, settings ?? _settings);
  }

  private const string ReorderedOld = """
        public class Sample
        {
            public bool Validate(int value)
            {
                if (value < 0)
                {
                    return false;
                }

                return true;
            }

            public string Describe()
            {
                return "sample";
            }
        }
        """;

  private const string ReorderedNew = """
        public class Sample
        {
            public string Describe()
            {
                return "sample";
            }

            public bool Validate(int value)
            {
                if (value < 0)
                {
                    return false;
                }

                return true;
            }
        }
        """;
}
