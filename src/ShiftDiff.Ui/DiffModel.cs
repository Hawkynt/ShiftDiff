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
// The Is* properties exist so a view can switch style classes per run without a
// converter for every combination.
public sealed record DiffSegment(string Text, DiffSegmentKind Kind, SourceTokenKind Syntax = SourceTokenKind.Identifier)
{
    public bool IsAdded => Kind == DiffSegmentKind.Added;

    public bool IsRemoved => Kind == DiffSegmentKind.Removed;

    public bool IsKeyword => Syntax == SourceTokenKind.Keyword;

    public bool IsString => Syntax == SourceTokenKind.String;

    public bool IsComment => Syntax == SourceTokenKind.Comment;

    public bool IsNumber => Syntax == SourceTokenKind.Number;

    public bool IsOperator => Syntax is SourceTokenKind.Operator or SourceTokenKind.Punctuation;
}

public sealed record DiffCell(int? LineNumber, IReadOnlyList<DiffSegment> Segments, CellState State)
{
    public static DiffCell Empty { get; } = new(null, [], CellState.Empty);

    public string Text => string.Concat(Segments.Select(segment => segment.Text));

    public string LineNumberText => LineNumber is { } number ? number.ToString() : string.Empty;

    public bool IsEmpty => State == CellState.Empty;

    public bool IsAdded => State == CellState.Added;

    public bool IsRemoved => State == CellState.Removed;

    public bool IsEdited => State == CellState.Edited;

    public bool IsMoved => State is CellState.Moved or CellState.MovedEdited;

    public bool IsConflict => State == CellState.Conflict;
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

    // A line that belongs to a relocated block reads as "moved" whichever side it
    // is on: showing it as a plain add/remove pair is exactly what this tool exists
    // to avoid.
    public ChangeType DisplayChangeType => IsMoved
        ? ChangeType == ChangeType.Edited ? ChangeType.MovedEdited : ChangeType.Moved
        : ChangeType;

    // Unchanged lines carry no marker — a gutter full of ticks is noise.
    public string Marker => IsChanged ? ChangeMarker.Text(DisplayChangeType) : string.Empty;

    public string EmojiMarker => IsChanged ? ChangeMarker.Emoji(DisplayChangeType) : string.Empty;

    public string Label => Kind == DiffRowKind.Collapsed
        ? $"{HiddenLineCount} unchanged line(s)"
        : ChangeMarker.Label(DisplayChangeType);

    public bool IsCollapsed => Kind == DiffRowKind.Collapsed;

    public bool IsLine => Kind == DiffRowKind.Line;

    public bool IsAdded => ChangeType == ChangeType.Added;

    public bool IsRemoved => ChangeType == ChangeType.Removed;

    public bool IsEdited => ChangeType == ChangeType.Edited;

    public string CollapsedText => $"⋯  {HiddenLineCount} unchanged lines  ⋯";
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

/// <summary>A thread drawn between two panes, connecting the two ends of one relocated block.</summary>
public sealed record PaneLink(int SourcePane, int TargetPane, int SourceRow, int TargetRow, ChangeType Kind);
