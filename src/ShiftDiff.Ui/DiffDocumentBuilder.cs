using ShiftDiff.Core;

namespace ShiftDiff.Ui;

// Turns engine output into rows a pane can draw: aligned cells, intra-line
// segments, folded unchanged regions, moved-block cross references.
public static class DiffDocumentBuilder {
  public static DiffDocument BuildTwoWay(
      SourceFileComparisonResult result,
      ComparisonSettings settings,
      string oldTitle = "Old",
      string newTitle = "New",
      IReadOnlySet<int>? expandedRegions = null) =>
      BuildTwoWay(result.Comparison, result.Language, settings, oldTitle, newTitle, expandedRegions);

  public static DiffDocument BuildTwoWay(
      FileComparisonResult comparison,
      SourceLanguage language,
      ComparisonSettings settings,
      string oldTitle = "Old",
      string newTitle = "New",
      IReadOnlySet<int>? expandedRegions = null) {
    // R-001: only blocks that genuinely changed reading order are announced.
    var blocks = MoveRefiner.Refine(comparison.MovedBlocks);
    var syntax = settings.SyntaxHighlighting ? language : SourceLanguage.PlainText;

    var rows = new List<DiffRow>(comparison.Changes.Length);
    foreach (var change in comparison.Changes) {
      var blockId = FindBlock(change, blocks);
      rows.Add(BuildRow(change, syntax, blockId));
    }

    SpreadMovedFlagAcrossRuns(rows, syntax);

    var collapsed = settings.CollapseUnchanged ? Collapse(rows, settings.ContextLines, 2, expandedRegions) : rows;
    collapsed = MarkChangeBlocks(collapsed, 2);
    var movedBlocks = BuildMovedBlockInfos(blocks, collapsed);
    var summary = Summarize(comparison.Changes, blocks.Length, 0);

    return new DiffDocument(collapsed, [oldTitle, newTitle], summary, movedBlocks, language);
  }

  public static DiffDocument BuildThreeWay(
      IReadOnlyList<ThreeWayChange> changes,
      ComparisonSettings settings,
      SourceLanguage language = SourceLanguage.PlainText,
      string baseTitle = "Base",
      string localTitle = "Local",
      string remoteTitle = "Remote",
      IReadOnlySet<int>? expandedRegions = null) {
    var rows = BuildThreeWayRows(changes, settings, language);
    var collapsed = MarkChangeBlocks(
        settings.CollapseUnchanged ? Collapse(rows, settings.ContextLines, 3, expandedRegions) : rows, 3);
    var summary = SummarizeThreeWay(changes);

    return new DiffDocument(collapsed, [baseTitle, localTitle, remoteTitle], summary, [], language);
  }

  // AC-003: the fourth pane is the candidate/reconstructed target, aligned to
  // the merge result the first three panes produce.
  public static DiffDocument BuildFourWay(
      IReadOnlyList<ThreeWayChange> changes,
      IReadOnlyList<string> targetLines,
      ComparisonSettings settings,
      SourceLanguage language = SourceLanguage.PlainText,
      string baseTitle = "Base",
      string localTitle = "Local",
      string remoteTitle = "Remote",
      string targetTitle = "Target",
      IReadOnlySet<int>? expandedRegions = null) {
    var threeWayRows = BuildThreeWayRows(changes, settings, language);
    var rows = new List<DiffRow>(threeWayRows.Count);
    var targetIndex = 0;

    foreach (var row in threeWayRows) {
      var expected = row.Cells[1].Text.Length > 0 ? row.Cells[1].Text : row.Cells[2].Text;
      var contributesLine = row.ChangeType != ChangeType.Removed && (row.Cells[1].LineNumber ?? row.Cells[2].LineNumber) is not null;

      DiffCell targetCell;
      if (!contributesLine || targetIndex >= targetLines.Count) {
        targetCell = DiffCell.Empty;
      } else {
        var text = targetLines[targetIndex];
        var matches = string.Equals(text, expected, StringComparison.Ordinal);
        targetCell = new DiffCell(
            targetIndex + 1,
            DiffSegmentBuilder.Build(text, null, false, settings.SyntaxHighlighting ? language : SourceLanguage.PlainText,
                matches ? DiffSegmentKind.Unchanged : DiffSegmentKind.Added),
            matches ? CellState.Unchanged : CellState.Edited);
        targetIndex++;
      }

      rows.Add(row with { Cells = [.. row.Cells, targetCell] });
    }

    var collapsed = MarkChangeBlocks(
        settings.CollapseUnchanged ? Collapse(rows, settings.ContextLines, 4, expandedRegions) : rows, 4);
    var summary = SummarizeThreeWay(changes);
    return new DiffDocument(
        collapsed, [baseTitle, localTitle, remoteTitle, targetTitle], summary, [], language);
  }

  private static List<DiffRow> BuildThreeWayRows(
      IReadOnlyList<ThreeWayChange> changes, ComparisonSettings settings, SourceLanguage language) {
    var syntax = settings.SyntaxHighlighting ? language : SourceLanguage.PlainText;
    var rows = new List<DiffRow>(changes.Count);

    foreach (var change in changes) {
      var state = change.ChangeType switch {
        ChangeType.Conflict => CellState.Conflict,
        ChangeType.Added => CellState.Added,
        ChangeType.Removed => CellState.Removed,
        ChangeType.Edited => CellState.Edited,
        _ => CellState.Unchanged,
      };

      rows.Add(new DiffRow(
          DiffRowKind.Line,
          change.ChangeType,
          [
              Cell(change.BaseLine, change.BaseIndex, CellState.Unchanged, syntax),
                    Cell(change.LocalLine, change.LocalIndex, SideState(change, ChangeSide.Local, state), syntax),
                    Cell(change.RemoteLine, change.RemoteIndex, SideState(change, ChangeSide.Remote, state), syntax),
          ],
          OldIndex: change.BaseIndex,
          NewIndex: change.LocalIndex));
    }

    return rows;
  }

  private static DiffSummary SummarizeThreeWay(IReadOnlyList<ThreeWayChange> changes) =>
      Summarize(
          changes.Select(change => new LineChange(change.ChangeType, change.BaseLine, change.LocalLine)).ToArray(),
          0,
          changes.Count(change => change.ChangeType == ChangeType.Conflict));

  /// <summary>Re-projects a side-by-side document into a single-column unified view (FR-042).</summary>
  public static DiffDocument ToUnified(DiffDocument document) {
    if (document.PaneCount != 2) return document;

    var rows = new List<DiffRow>(document.Rows.Count);
    foreach (var row in document.Rows) {
      if (row.Kind == DiffRowKind.Collapsed) {
        rows.Add(row with { Cells = [row.Cells[0]] });
        continue;
      }

      switch (row.ChangeType) {
        case ChangeType.Edited:
          rows.Add(row with { ChangeType = ChangeType.Removed, Cells = [row.Cells[0]] });
          rows.Add(row with { ChangeType = ChangeType.Added, Cells = [row.Cells[1]] });
          break;
        case ChangeType.Removed:
          rows.Add(row with { Cells = [row.Cells[0]] });
          break;
        default:
          rows.Add(row with { Cells = [row.Cells[1].State == CellState.Empty ? row.Cells[0] : row.Cells[1]] });
          break;
      }
    }

    return new DiffDocument(
        rows, [$"{document.PaneTitles[0]} → {document.PaneTitles[1]}"], document.Summary, document.MovedBlocks, document.Language);
  }

  // A relocated method usually matches only on its distinctive lines — braces
  // and blank lines are never anchors (AnchorDetector). Displaying half a
  // method as "moved" and the other half as "added" is worse than useless, so
  // one contiguous run of adds (or removes) inherits the move flag as a whole.
  // The reported block ranges in the inspector stay exactly what the engine
  // found.
  private static void SpreadMovedFlagAcrossRuns(List<DiffRow> rows, SourceLanguage syntax) {
    var index = 0;
    while (index < rows.Count) {
      var type = rows[index].ChangeType;
      if (type is not (ChangeType.Added or ChangeType.Removed)) {
        index++;
        continue;
      }

      var start = index;
      while (index < rows.Count && rows[index].ChangeType == type) index++;

      var blockId = rows.Skip(start).Take(index - start)
          .FirstOrDefault(row => row.MovedBlockId is not null)?.MovedBlockId;
      if (blockId is null) continue;

      for (var i = start; i < index; i++) {
        if (rows[i].IsMoved) continue;

        var cellIndex = type == ChangeType.Added ? 1 : 0;
        var cells = rows[i].Cells.ToArray();
        cells[cellIndex] = cells[cellIndex] with { State = CellState.Moved };
        rows[i] = rows[i] with { IsMoved = true, MovedBlockId = blockId, Cells = cells };
      }
    }
  }

  // Araxis-style change blocks: every run of consecutive changed rows becomes
  // one identified block, each pane's cell knows where it sits inside the box,
  // and the panes that are not the merge result carry a transfer arrow on the
  // block's first row.
  private static IReadOnlyList<DiffRow> MarkChangeBlocks(IReadOnlyList<DiffRow> rows, int paneCount) {
    var marked = rows.ToArray();

    // The reconstructed result mirrors the second pane, so that is where a
    // transferred block lands and the arrows point at it.
    const int resultPane = 1;

    var blockId = 0;
    var index = 0;
    while (index < marked.Length) {
      if (!marked[index].IsChanged) {
        marked[index] = WithPaneInfo(marked[index], paneCount);
        index++;
        continue;
      }

      var start = index;
      while (index < marked.Length && marked[index].IsChanged) index++;

      for (var row = start; row < index; row++) {
        var edge = (start == index - 1, row == start, row == index - 1) switch {
          (true, _, _) => BlockEdge.Single,
          (_, true, _) => BlockEdge.First,
          (_, _, true) => BlockEdge.Last,
          _ => BlockEdge.Middle,
        };

        marked[row] = WithPaneInfo(
            marked[row] with { BlockId = blockId, Edge = edge }, paneCount, blockId, edge, resultPane);
      }

      blockId++;
    }

    return marked;
  }

  private static DiffRow WithPaneInfo(
      DiffRow row, int paneCount, int? blockId = null, BlockEdge edge = BlockEdge.None, int resultPane = 1) {
    var cells = new DiffCell[row.Cells.Count];
    for (var pane = 0; pane < row.Cells.Count; pane++) {
      var isLastPane = pane == row.Cells.Count - 1;
      var canTransfer = blockId is not null
          && edge is BlockEdge.First or BlockEdge.Single
          && pane != resultPane
          && paneCount > 1
          && paneCount != 4;

      // A pane with no line here still has a version of the block — the
      // empty one — so taking it removes those lines from the result.
      var removesLines = row.Cells[pane].State == CellState.Empty;

      cells[pane] = row.Cells[pane] with {
        PaneIndex = pane,
        BlockId = blockId,
        Edge = edge,
        CanTransfer = canTransfer,
        TransferGlyph = pane < resultPane ? "▶" : "◀",
        TransferTip = canTransfer
              ? removesLines ? "Drop this block from the result" : "Use this block in the result"
              : string.Empty,
        IsLastPane = isLastPane,
      };
    }

    return row with { Cells = cells };
  }

  private static DiffRow BuildRow(LineChange change, SourceLanguage syntax, int? blockId) {
    var moved = blockId is not null;

    return change.ChangeType switch {
      // The cell background already says "this whole line is new"; strong
      // per-token highlighting is reserved for edits inside a shared line.
      ChangeType.Added => new DiffRow(
          DiffRowKind.Line, ChangeType.Added,
          [DiffCell.Empty, Cell(change.NewLine, change.NewIndex, moved ? CellState.Moved : CellState.Added, syntax)],
          moved, MovedBlockId: blockId, OldIndex: change.OldIndex, NewIndex: change.NewIndex),

      ChangeType.Removed => new DiffRow(
          DiffRowKind.Line, ChangeType.Removed,
          [Cell(change.OldLine, change.OldIndex, moved ? CellState.Moved : CellState.Removed, syntax), DiffCell.Empty],
          moved, MovedBlockId: blockId, OldIndex: change.OldIndex, NewIndex: change.NewIndex),

      ChangeType.Edited => new DiffRow(
          DiffRowKind.Line, ChangeType.Edited,
          [
              new DiffCell(
                        change.OldIndex + 1,
                        DiffSegmentBuilder.Build(change.OldLine, change.TokenChanges, true, syntax),
                        moved ? CellState.MovedEdited : CellState.Edited),
                    new DiffCell(
                        change.NewIndex + 1,
                        DiffSegmentBuilder.Build(change.NewLine, change.TokenChanges, false, syntax),
                        moved ? CellState.MovedEdited : CellState.Edited),
          ],
          moved, MovedBlockId: blockId, OldIndex: change.OldIndex, NewIndex: change.NewIndex),

      _ => new DiffRow(
          DiffRowKind.Line, ChangeType.Unchanged,
          [
              Cell(change.OldLine, change.OldIndex, moved ? CellState.Moved : CellState.Unchanged, syntax),
                    Cell(change.NewLine, change.NewIndex, moved ? CellState.Moved : CellState.Unchanged, syntax),
          ],
          moved, MovedBlockId: blockId, OldIndex: change.OldIndex, NewIndex: change.NewIndex),
    };
  }

  private static DiffCell Cell(
      string? text, int? index, CellState state, SourceLanguage syntax,
      DiffSegmentKind fallback = DiffSegmentKind.Unchanged) =>
      text is null && index is null
          ? DiffCell.Empty
          : new DiffCell(index + 1, DiffSegmentBuilder.Build(text, null, false, syntax, fallback), state);

  private static CellState SideState(ThreeWayChange change, ChangeSide side, CellState state) =>
      change.ChangeType == ChangeType.Conflict || change.Side == ChangeSide.Both || change.Side == side
          ? state
          : CellState.Unchanged;

  private static int? FindBlock(LineChange change, IReadOnlyList<BlockMatch> blocks) {
    for (var i = 0; i < blocks.Count; i++) {
      var block = blocks[i];
      if (change.OldIndex is { } oldIndex && oldIndex >= block.OldStart && oldIndex <= block.OldEnd) return i;
      if (change.NewIndex is { } newIndex && newIndex >= block.NewStart && newIndex <= block.NewEnd) return i;
    }

    return null;
  }

  private static IReadOnlyList<MovedBlockInfo> BuildMovedBlockInfos(
      IReadOnlyList<BlockMatch> blocks, IReadOnlyList<DiffRow> rows) {
    var infos = new List<MovedBlockInfo>(blocks.Count);
    for (var i = 0; i < blocks.Count; i++) {
      var block = blocks[i];
      infos.Add(new MovedBlockInfo(
          i,
          block.MatchType,
          block.Confidence,
          block.Score,
          block.OldStart,
          block.OldEnd,
          block.NewStart,
          block.NewEnd,
          FindRow(rows, row => row.OldIndex == block.OldStart),
          FindRow(rows, row => row.NewIndex == block.NewStart)));
    }

    return infos;
  }

  private static int FindRow(IReadOnlyList<DiffRow> rows, Func<DiffRow, bool> predicate) {
    for (var i = 0; i < rows.Count; i++) {
      if (predicate(rows[i])) return i;
    }

    return -1;
  }

  // Folds runs of unchanged rows that are further than `context` lines from any
  // change into a single expandable row.
  private static IReadOnlyList<DiffRow> Collapse(
      IReadOnlyList<DiffRow> rows, int context, int paneCount, IReadOnlySet<int>? expandedRegions = null) {
    if (rows.Count == 0) return rows;

    var keep = new bool[rows.Count];
    for (var i = 0; i < rows.Count; i++) {
      if (!rows[i].IsChanged) continue;

      for (var j = Math.Max(0, i - context); j <= Math.Min(rows.Count - 1, i + context); j++) keep[j] = true;
    }

    var result = new List<DiffRow>(rows.Count);
    var index = 0;
    while (index < rows.Count) {
      if (keep[index]) {
        result.Add(rows[index]);
        index++;
        continue;
      }

      var start = index;
      while (index < rows.Count && !keep[index]) index++;

      var hidden = index - start;

      // A one-line gap costs more to fold than to show, and a region the
      // user expanded by hand stays open.
      if (hidden <= 1 || expandedRegions?.Contains(ShellViewModel.FoldedRegionKey(rows[start])) == true) {
        for (var i = start; i < index; i++) result.Add(rows[i]);
        continue;
      }

      result.Add(new DiffRow(
          DiffRowKind.Collapsed,
          ChangeType.Unchanged,
          [.. Enumerable.Repeat(DiffCell.Empty, paneCount)],
          HiddenLineCount: hidden,
          OldIndex: rows[start].OldIndex,
          NewIndex: rows[start].NewIndex));
    }

    return result;
  }

  private static DiffSummary Summarize(IReadOnlyList<LineChange> changes, int movedBlocks, int conflicts) =>
      new(
          changes.Count(change => change.ChangeType == ChangeType.Added),
          changes.Count(change => change.ChangeType == ChangeType.Removed),
          changes.Count(change => change.ChangeType == ChangeType.Edited),
          changes.Count(change => change.ChangeType == ChangeType.Unchanged),
          movedBlocks,
          conflicts);
}
