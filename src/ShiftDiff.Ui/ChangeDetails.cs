using ShiftDiff.Core;

namespace ShiftDiff.Ui;

public sealed record DetailEntry(string Label, string Value);

// FR-046 Change Details Panel: why this block was matched, where it came from,
// where it went, and how sure the engine is.
public sealed record ChangeDetails(string Title, string Subtitle, IReadOnlyList<DetailEntry> Entries) {
  public static ChangeDetails Empty { get; } = new("No selection", "Select a line to inspect it", []);
}

public static class ChangeDetailsBuilder {
  public static ChangeDetails Build(DiffDocument document, int rowIndex) {
    if (rowIndex < 0 || rowIndex >= document.Rows.Count) return ChangeDetails.Empty;

    var row = document.Rows[rowIndex];
    if (row.Kind == DiffRowKind.Collapsed) {
      return new ChangeDetails(
          "Folded region",
          $"{row.HiddenLineCount} unchanged line(s)",
          [new DetailEntry("Action", "Click to expand this region")]);
    }

    var entries = new List<DetailEntry>
    {
            new("Change type", ChangeMarker.Label(row.DisplayChangeType)),
        };

    if (row.OldIndex is { } oldIndex) entries.Add(new DetailEntry("Source line", (oldIndex + 1).ToString()));
    if (row.NewIndex is { } newIndex) entries.Add(new DetailEntry("Target line", (newIndex + 1).ToString()));

    var block = row.MovedBlockId is { } id
        ? document.MovedBlocks.FirstOrDefault(candidate => candidate.Id == id)
        : null;

    if (block is not null) {
      entries.Add(new DetailEntry("Moved block", block.Range));
      entries.Add(new DetailEntry("Block size", $"{block.LineCount} line(s)"));
      entries.Add(new DetailEntry("Similarity score", block.ScoreText));
      entries.Add(new DetailEntry("Confidence", ChangeMarker.Label(block.Confidence)));
      entries.Add(new DetailEntry("Match reason", Explain(block)));
    }

    var edited = row.Cells.SelectMany(cell => cell.Segments)
        .Count(segment => segment.Kind is DiffSegmentKind.Added or DiffSegmentKind.Removed);
    if (edited > 0) entries.Add(new DetailEntry("Changed token runs", edited.ToString()));

    var title = block is not null ? block.Title : ChangeMarker.Label(row.DisplayChangeType);
    var subtitle = block is not null ? block.Range : Describe(row);
    return new ChangeDetails(title, subtitle, entries);
  }

  // R-001 mitigation: the user must be able to see why a match was accepted.
  private static string Explain(MovedBlockInfo block) => block.Confidence switch {
    Confidence.Certain =>
        $"{block.LineCount} consecutive lines matched at {block.ScoreText} similarity and appear out of reading order",
    Confidence.Likely =>
        $"strong content overlap ({block.ScoreText}) with a consistent displacement across {block.LineCount} line(s)",
    Confidence.Possible =>
        $"partial content overlap ({block.ScoreText}); anchors are weaker than for a certain match",
    Confidence.Weak =>
        $"weak evidence ({block.ScoreText}) — mostly low-value anchor lines; treat with care",
    _ => "match rejected by the confidence classifier",
  };

  private static string Describe(DiffRow row) => row.DisplayChangeType switch {
    ChangeType.Added => "Only present in the target",
    ChangeType.Removed => "Only present in the source",
    ChangeType.Edited => "Present on both sides with token-level changes",
    ChangeType.Conflict => "Both sides changed this line",
    _ => "Identical on both sides",
  };
}
