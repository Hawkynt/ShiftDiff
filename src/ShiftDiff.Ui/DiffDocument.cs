using ShiftDiff.Core;

namespace ShiftDiff.Ui;

// SPEC 13.1 Document Model: everything a pane, the overview bar, the navigator
// and the inspector need, computed once per comparison.
public sealed class DiffDocument {
  public static DiffDocument Empty { get; } = new([], ["Old", "New"], new DiffSummary(0, 0, 0, 0, 0, 0), [], SourceLanguage.PlainText);

  public DiffDocument(
      IReadOnlyList<DiffRow> rows,
      IReadOnlyList<string> paneTitles,
      DiffSummary summary,
      IReadOnlyList<MovedBlockInfo> movedBlocks,
      SourceLanguage language) {
    Rows = rows;
    PaneTitles = paneTitles;
    Summary = summary;
    MovedBlocks = movedBlocks;
    Language = language;
    Overview = OverviewBuilder.Build(rows);
    Links = BuildLinks(rows, movedBlocks, PaneTitles.Count);
    ChangeRowIndices = FirstRowsOfEachRun(rows, row => row.IsChanged);
    ConflictRowIndices = FirstRowsOfEachRun(rows, row => row.IsConflict);
    MovedRowIndices = FirstRowsOfEachRun(rows, row => row.IsMoved);
  }

  public IReadOnlyList<DiffRow> Rows { get; }

  public IReadOnlyList<string> PaneTitles { get; }

  public DiffSummary Summary { get; }

  public IReadOnlyList<MovedBlockInfo> MovedBlocks { get; }

  public SourceLanguage Language { get; }

  public IReadOnlyList<OverviewStripe> Overview { get; }

  /// <summary>Connector threads between panes for every relocated block (Araxis-style linking).</summary>
  public IReadOnlyList<PaneLink> Links { get; }

  /// <summary>Row index of the first line of each contiguous run of changes (FR-045 next/previous change).</summary>
  public IReadOnlyList<int> ChangeRowIndices { get; }

  public IReadOnlyList<int> ConflictRowIndices { get; }

  public IReadOnlyList<int> MovedRowIndices { get; }

  public int PaneCount => PaneTitles.Count;

  public string LanguageName => SourceLanguageDetector.GetDisplayName(Language);

  // One ribbon per change block, drawn in the gutter between each pair of
  // neighbouring panes. Aligned blocks give a straight band; a relocated block
  // is joined to the rows its counterpart occupies, so the ribbon slopes.
  private static IReadOnlyList<PaneLink> BuildLinks(
      IReadOnlyList<DiffRow> rows, IReadOnlyList<MovedBlockInfo> movedBlocks, int paneCount) {
    if (paneCount < 2) return [];

    var links = new List<PaneLink>();

    // A relocated block is one connector spanning both of its ends, so the
    // rows it covers are taken out of the per-block pass below — otherwise
    // each end would be bracketed twice, once short and once tall.
    var relocated = new HashSet<int>();
    foreach (var block in movedBlocks) {
      var source = RowsOfMovedBlock(rows, block.Id, pane: 0);
      var target = RowsOfMovedBlock(rows, block.Id, pane: 1);
      if (source is null || target is null) continue;

      links.Add(new PaneLink(
          0, 1, source.Value.Start, source.Value.End, target.Value.Start, target.Value.End, block.MatchType));

      for (var row = 0; row < rows.Count; row++) {
        if (rows[row].MovedBlockId == block.Id) relocated.Add(row);
      }
    }

    var index = 0;
    while (index < rows.Count) {
      if (!rows[index].IsChanged) {
        index++;
        continue;
      }

      var start = index;
      while (index < rows.Count && rows[index].IsChanged) index++;

      var end = index - 1;
      var kind = rows[start].DisplayChangeType;

      if (Enumerable.Range(start, end - start + 1).All(relocated.Contains)) continue;

      for (var pane = 0; pane + 1 < paneCount; pane++) {
        var side = Side(rows, start, end, pane);
        var other = Side(rows, start, end, pane + 1);
        if (side is null && other is null) continue;

        // A pane that contributes nothing to the block (a pure insertion
        // or deletion) collapses to a point, so the ribbon reads as a
        // wedge pointing at where the lines went or came from.
        var (sourceStart, sourceEnd) = side ?? (other!.Value.Start, other.Value.Start);
        var (targetStart, targetEnd) = other ?? (side!.Value.Start, side.Value.Start);
        links.Add(new PaneLink(pane, pane + 1, sourceStart, sourceEnd, targetStart, targetEnd, kind));
      }
    }

    return links;
  }

  // The rows one relocated block occupies in a pane — its whole extent, so the
  // bracket grows with the block instead of being repeated per line.
  private static (int Start, int End)? RowsOfMovedBlock(IReadOnlyList<DiffRow> rows, int blockId, int pane) {
    int? first = null;
    var last = 0;
    for (var i = 0; i < rows.Count; i++) {
      if (rows[i].MovedBlockId != blockId) continue;
      if (rows[i].Cells.Count <= pane || rows[i].Cells[pane].State == CellState.Empty) continue;

      first ??= i;
      last = i;
    }

    return first is { } value ? (value, last) : null;
  }

  // The rows this pane actually contributes inside the block; null when the
  // pane is empty there (a pure insertion or deletion).
  private static (int Start, int End)? Side(IReadOnlyList<DiffRow> rows, int start, int end, int pane) {
    int? first = null;
    var last = start;
    for (var i = start; i <= end; i++) {
      if (rows[i].Cells.Count <= pane || rows[i].Cells[pane].State == CellState.Empty) continue;

      first ??= i;
      last = i;
    }

    return first is { } value ? (value, last) : null;
  }

  // Groups consecutive matching rows so navigation stops once per change
  // block instead of once per line.
  private static IReadOnlyList<int> FirstRowsOfEachRun(IReadOnlyList<DiffRow> rows, Func<DiffRow, bool> predicate) {
    var indices = new List<int>();
    var inRun = false;
    for (var i = 0; i < rows.Count; i++) {
      if (!predicate(rows[i])) {
        inRun = false;
        continue;
      }

      if (!inRun) indices.Add(i);
      inRun = true;
    }

    return indices;
  }
}
