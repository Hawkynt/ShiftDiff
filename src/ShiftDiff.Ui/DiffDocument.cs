using ShiftDiff.Core;

namespace ShiftDiff.Ui;

// SPEC 13.1 Document Model: everything a pane, the overview bar, the navigator
// and the inspector need, computed once per comparison.
public sealed class DiffDocument
{
    public static DiffDocument Empty { get; } = new([], ["Old", "New"], new DiffSummary(0, 0, 0, 0, 0, 0), [], SourceLanguage.PlainText);

    public DiffDocument(
        IReadOnlyList<DiffRow> rows,
        IReadOnlyList<string> paneTitles,
        DiffSummary summary,
        IReadOnlyList<MovedBlockInfo> movedBlocks,
        SourceLanguage language)
    {
        Rows = rows;
        PaneTitles = paneTitles;
        Summary = summary;
        MovedBlocks = movedBlocks;
        Language = language;
        Overview = OverviewBuilder.Build(rows);
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

    /// <summary>Row index of the first line of each contiguous run of changes (FR-045 next/previous change).</summary>
    public IReadOnlyList<int> ChangeRowIndices { get; }

    public IReadOnlyList<int> ConflictRowIndices { get; }

    public IReadOnlyList<int> MovedRowIndices { get; }

    public int PaneCount => PaneTitles.Count;

    public string LanguageName => SourceLanguageDetector.GetDisplayName(Language);

    // Groups consecutive matching rows so navigation stops once per change
    // block instead of once per line.
    private static IReadOnlyList<int> FirstRowsOfEachRun(IReadOnlyList<DiffRow> rows, Func<DiffRow, bool> predicate)
    {
        var indices = new List<int>();
        var inRun = false;
        for (var i = 0; i < rows.Count; i++)
        {
            if (!predicate(rows[i]))
            {
                inRun = false;
                continue;
            }

            if (!inRun) indices.Add(i);
            inRun = true;
        }

        return indices;
    }
}
