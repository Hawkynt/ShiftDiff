using ShiftDiff.Core;

namespace ShiftDiff.Ui;

// Turns engine output into rows a pane can draw: aligned cells, intra-line
// segments, folded unchanged regions, moved-block cross references.
public static class DiffDocumentBuilder
{
    public static DiffDocument BuildTwoWay(
        SourceFileComparisonResult result,
        ComparisonSettings settings,
        string oldTitle = "Old",
        string newTitle = "New") =>
        BuildTwoWay(result.Comparison, result.Language, settings, oldTitle, newTitle);

    public static DiffDocument BuildTwoWay(
        FileComparisonResult comparison,
        SourceLanguage language,
        ComparisonSettings settings,
        string oldTitle = "Old",
        string newTitle = "New")
    {
        // R-001: only blocks that genuinely changed reading order are announced.
        var blocks = MoveRefiner.Refine(comparison.MovedBlocks);
        var syntax = settings.SyntaxHighlighting ? language : SourceLanguage.PlainText;

        var rows = new List<DiffRow>(comparison.Changes.Length);
        foreach (var change in comparison.Changes)
        {
            var blockId = FindBlock(change, blocks);
            rows.Add(BuildRow(change, syntax, blockId));
        }

        var collapsed = settings.CollapseUnchanged ? Collapse(rows, settings.ContextLines, 2) : rows;
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
        string remoteTitle = "Remote")
    {
        var rows = BuildThreeWayRows(changes, settings, language);
        var collapsed = settings.CollapseUnchanged ? Collapse(rows, settings.ContextLines, 3) : rows;
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
        string targetTitle = "Target")
    {
        var threeWayRows = BuildThreeWayRows(changes, settings, language);
        var rows = new List<DiffRow>(threeWayRows.Count);
        var targetIndex = 0;

        foreach (var row in threeWayRows)
        {
            var expected = row.Cells[1].Text.Length > 0 ? row.Cells[1].Text : row.Cells[2].Text;
            var contributesLine = row.ChangeType != ChangeType.Removed && (row.Cells[1].LineNumber ?? row.Cells[2].LineNumber) is not null;

            DiffCell targetCell;
            if (!contributesLine || targetIndex >= targetLines.Count)
            {
                targetCell = DiffCell.Empty;
            }
            else
            {
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

        var collapsed = settings.CollapseUnchanged ? Collapse(rows, settings.ContextLines, 4) : rows;
        var summary = SummarizeThreeWay(changes);
        return new DiffDocument(
            collapsed, [baseTitle, localTitle, remoteTitle, targetTitle], summary, [], language);
    }

    private static List<DiffRow> BuildThreeWayRows(
        IReadOnlyList<ThreeWayChange> changes, ComparisonSettings settings, SourceLanguage language)
    {
        var syntax = settings.SyntaxHighlighting ? language : SourceLanguage.PlainText;
        var rows = new List<DiffRow>(changes.Count);

        foreach (var change in changes)
        {
            var state = change.ChangeType switch
            {
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
    public static DiffDocument ToUnified(DiffDocument document)
    {
        if (document.PaneCount != 2) return document;

        var rows = new List<DiffRow>(document.Rows.Count);
        foreach (var row in document.Rows)
        {
            if (row.Kind == DiffRowKind.Collapsed)
            {
                rows.Add(row with { Cells = [row.Cells[0]] });
                continue;
            }

            switch (row.ChangeType)
            {
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

    private static DiffRow BuildRow(LineChange change, SourceLanguage syntax, int? blockId)
    {
        var moved = blockId is not null;

        return change.ChangeType switch
        {
            ChangeType.Added => new DiffRow(
                DiffRowKind.Line, ChangeType.Added,
                [DiffCell.Empty, Cell(change.NewLine, change.NewIndex, CellState.Added, syntax, DiffSegmentKind.Added)],
                moved, MovedBlockId: blockId, OldIndex: change.OldIndex, NewIndex: change.NewIndex),

            ChangeType.Removed => new DiffRow(
                DiffRowKind.Line, ChangeType.Removed,
                [Cell(change.OldLine, change.OldIndex, CellState.Removed, syntax, DiffSegmentKind.Removed), DiffCell.Empty],
                moved, MovedBlockId: blockId, OldIndex: change.OldIndex, NewIndex: change.NewIndex),

            ChangeType.Edited => new DiffRow(
                DiffRowKind.Line, ChangeType.Edited,
                [
                    new DiffCell(
                        change.OldIndex + 1,
                        DiffSegmentBuilder.Build(change.OldLine, change.TokenChanges, true, syntax),
                        CellState.Edited),
                    new DiffCell(
                        change.NewIndex + 1,
                        DiffSegmentBuilder.Build(change.NewLine, change.TokenChanges, false, syntax),
                        CellState.Edited),
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

    private static int? FindBlock(LineChange change, IReadOnlyList<BlockMatch> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (change.OldIndex is { } oldIndex && oldIndex >= block.OldStart && oldIndex <= block.OldEnd) return i;
            if (change.NewIndex is { } newIndex && newIndex >= block.NewStart && newIndex <= block.NewEnd) return i;
        }

        return null;
    }

    private static IReadOnlyList<MovedBlockInfo> BuildMovedBlockInfos(
        IReadOnlyList<BlockMatch> blocks, IReadOnlyList<DiffRow> rows)
    {
        var infos = new List<MovedBlockInfo>(blocks.Count);
        for (var i = 0; i < blocks.Count; i++)
        {
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

    private static int FindRow(IReadOnlyList<DiffRow> rows, Func<DiffRow, bool> predicate)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (predicate(rows[i])) return i;
        }

        return -1;
    }

    // Folds runs of unchanged rows that are further than `context` lines from any
    // change into a single expandable row.
    private static IReadOnlyList<DiffRow> Collapse(IReadOnlyList<DiffRow> rows, int context, int paneCount)
    {
        if (rows.Count == 0) return rows;

        var keep = new bool[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            if (!rows[i].IsChanged) continue;

            for (var j = Math.Max(0, i - context); j <= Math.Min(rows.Count - 1, i + context); j++) keep[j] = true;
        }

        var result = new List<DiffRow>(rows.Count);
        var index = 0;
        while (index < rows.Count)
        {
            if (keep[index])
            {
                result.Add(rows[index]);
                index++;
                continue;
            }

            var start = index;
            while (index < rows.Count && !keep[index]) index++;

            var hidden = index - start;

            // A one-line gap costs more to fold than to show.
            if (hidden <= 1)
            {
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
