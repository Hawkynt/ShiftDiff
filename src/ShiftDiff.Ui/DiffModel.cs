using ShiftDiff.Core;

namespace ShiftDiff.Ui;

/// <summary>How a run of characters inside one displayed line changed.</summary>
public enum DiffSegmentKind
{
    Unchanged,
    Added,
    Removed,
    Edited,
}

/// <summary>What one pane shows for one row.</summary>
public enum CellState
{
    /// <summary>The pane contributes no line here (the other side is longer).</summary>
    Empty,
    Unchanged,
    Added,
    Removed,
    Edited,
    Moved,
    MovedEdited,
    Conflict,
}

public enum DiffRowKind
{
    Line,

    /// <summary>A folded run of unchanged lines the user can expand.</summary>
    Collapsed,
}

/// <summary>A run of characters with both its diff state and its syntax class.</summary>
public sealed record DiffSegment(string Text, DiffSegmentKind Kind, SourceTokenKind Syntax = SourceTokenKind.Identifier);

public sealed record DiffCell(int? LineNumber, IReadOnlyList<DiffSegment> Segments, CellState State)
{
    public static DiffCell Empty { get; } = new(null, [], CellState.Empty);

    public string Text => string.Concat(Segments.Select(segment => segment.Text));

    public string LineNumberText => LineNumber is { } number ? number.ToString() : string.Empty;
}

/// <summary>One displayed row across every pane of the current layout.</summary>
public sealed record DiffRow(
    DiffRowKind Kind,
    ChangeType ChangeType,
    IReadOnlyList<DiffCell> Cells,
    bool IsMoved = false,
    int HiddenLineCount = 0,
    int? MovedBlockId = null,
    int? OldIndex = null,
    int? NewIndex = null)
{
    public bool IsChanged => Kind == DiffRowKind.Line && (ChangeType != ChangeType.Unchanged || IsMoved);

    public bool IsConflict => ChangeType == ChangeType.Conflict;

    public ChangeType DisplayChangeType => IsMoved && ChangeType == ChangeType.Unchanged ? ChangeType.Moved : ChangeType;

    public string Marker => ChangeMarker.Text(DisplayChangeType);

    public string EmojiMarker => ChangeMarker.Emoji(DisplayChangeType);

    public string Label => Kind == DiffRowKind.Collapsed
        ? $"{HiddenLineCount} unchanged line(s)"
        : ChangeMarker.Label(DisplayChangeType);
}

/// <summary>A moved block plus the row indices where each of its ends is shown (FR-045 jump-to-pair).</summary>
public sealed record MovedBlockInfo(
    int Id,
    ChangeType MatchType,
    Confidence Confidence,
    double Score,
    int OldStart,
    int OldEnd,
    int NewStart,
    int NewEnd,
    int OldRowIndex,
    int NewRowIndex)
{
    public string Title => $"{ChangeMarker.Label(MatchType)} · {ChangeMarker.Label(Confidence)}";

    public string Range => $"old {OldStart + 1}–{OldEnd + 1} → new {NewStart + 1}–{NewEnd + 1}";

    public string ScoreText => Score.ToString("P0");

    public int LineCount => OldEnd - OldStart + 1;
}

public sealed record DiffSummary(
    int Added,
    int Removed,
    int Edited,
    int Unchanged,
    int MovedBlocks,
    int Conflicts)
{
    public int TotalChanges => Added + Removed + Edited + Conflicts;

    public bool HasDifferences => TotalChanges > 0 || MovedBlocks > 0;

    public string Text =>
        $"{Added} added · {Removed} removed · {Edited} edited · {MovedBlocks} moved" +
        (Conflicts > 0 ? $" · {Conflicts} conflicts" : string.Empty);
}

/// <summary>One stripe of the overview bar / minimap, positioned in normalized 0..1 document space.</summary>
public sealed record OverviewStripe(double Start, double End, ChangeType ChangeType, int RowIndex);
