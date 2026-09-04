using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

public class ChangeNavigatorTests {
  [Fact]
  public void Next_FromBeforeTheFirstChange_LandsOnTheFirstChange() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt(3, 7));

    Assert.Equal(3, navigator.Next(-1));
  }

  [Fact]
  public void Next_FromInsideAChange_SkipsToTheNextChangeRun() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt(3, 7));

    Assert.Equal(7, navigator.Next(3));
  }

  [Fact]
  public void Next_FromTheLastChange_WrapsToTheFirst() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt(3, 7));

    Assert.Equal(3, navigator.Next(7));
  }

  [Fact]
  public void Previous_FromTheFirstChange_WrapsToTheLast() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt(3, 7));

    Assert.Equal(7, navigator.Previous(3));
  }

  [Fact]
  public void Next_WithNoChanges_ReturnsMinusOne() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt());

    Assert.Equal(-1, navigator.Next(0));
    Assert.Equal(-1, navigator.Previous(0));
  }

  [Fact]
  public void FirstAndLast_PointAtTheOuterChanges() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt(3, 7));

    Assert.Equal(3, navigator.First);
    Assert.Equal(7, navigator.Last);
  }

  [Fact]
  public void ConsecutiveChangedRows_CountAsOneNavigationStop() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt(3, 4, 5));

    Assert.Single(navigator.Document.ChangeRowIndices);
  }

  [Fact]
  public void OrdinalOf_ReportsThePositionOfTheChangeUnderTheCursor() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt(3, 7));

    Assert.Equal(1, navigator.OrdinalOf(3));
    Assert.Equal(2, navigator.OrdinalOf(7));
    Assert.Equal(0, navigator.OrdinalOf(5));
  }

  [Fact]
  public void NextConflict_VisitsOnlyConflictRows() {
    var rows = new List<DiffRow>();
    for (var i = 0; i < 6; i++) rows.Add(Row(ChangeType.Unchanged));
    rows[2] = Row(ChangeType.Edited);
    rows[4] = Row(ChangeType.Conflict);
    var navigator = new ChangeNavigator(Document(rows));

    Assert.Equal(4, navigator.NextConflict(0));
    Assert.Equal(4, navigator.PreviousConflict(5));
  }

  [Fact]
  public void NextMoved_VisitsOnlyRowsBelongingToAMovedBlock() {
    var rows = new List<DiffRow>();
    for (var i = 0; i < 6; i++) rows.Add(Row(ChangeType.Unchanged));
    rows[3] = Row(ChangeType.Unchanged) with { IsMoved = true, MovedBlockId = 0 };
    var navigator = new ChangeNavigator(Document(rows));

    Assert.Equal(3, navigator.NextMoved(0));
    Assert.Equal(3, navigator.PreviousMoved(5));
  }

  [Fact]
  public void PairedRow_FromOneEndOfAMovedBlock_JumpsToTheOther() {
    var rows = new List<DiffRow>();
    for (var i = 0; i < 10; i++) rows.Add(Row(ChangeType.Unchanged));
    rows[2] = Row(ChangeType.Removed) with { IsMoved = true, MovedBlockId = 0, OldIndex = 2 };
    rows[8] = Row(ChangeType.Added) with { IsMoved = true, MovedBlockId = 0, NewIndex = 8 };
    var blocks = new[] { new MovedBlockInfo(0, ChangeType.Moved, Confidence.Certain, 0.9, 2, 3, 8, 9, 2, 8) };
    var navigator = new ChangeNavigator(Document(rows, blocks));

    Assert.Equal(8, navigator.PairedRow(2));
    Assert.Equal(2, navigator.PairedRow(8));
  }

  [Fact]
  public void PairedRow_OnARowThatIsNotPartOfAMove_ReturnsMinusOne() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt(3));

    Assert.Equal(-1, navigator.PairedRow(3));
  }

  [Fact]
  public void PairedRow_OutsideTheDocument_ReturnsMinusOne() {
    var navigator = new ChangeNavigator(DocumentWithChangesAt(3));

    Assert.Equal(-1, navigator.PairedRow(999));
  }

  private static DiffDocument DocumentWithChangesAt(params int[] changedRows) {
    var rows = new List<DiffRow>();
    for (var i = 0; i < 10; i++) rows.Add(Row(changedRows.Contains(i) ? ChangeType.Edited : ChangeType.Unchanged));
    return Document(rows);
  }

  private static DiffDocument Document(IReadOnlyList<DiffRow> rows, IReadOnlyList<MovedBlockInfo>? blocks = null) =>
      new(rows, ["Old", "New"], new DiffSummary(0, 0, 0, 0, blocks?.Count ?? 0, 0), blocks ?? [], SourceLanguage.PlainText);

  private static DiffRow Row(ChangeType type) =>
      new(DiffRowKind.Line, type, [DiffCell.Empty, DiffCell.Empty]);
}
