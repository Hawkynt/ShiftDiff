using ShiftDiff.Core;

namespace ShiftDiff.Ui;

[Flags]
public enum ChangeTypeFilter
{
    None = 0,
    Unchanged = 1,
    Added = 2,
    Removed = 4,
    Edited = 8,
    Moved = 16,
    Conflict = 32,
    All = Unchanged | Added | Removed | Edited | Moved | Conflict,
    OnlyChanges = Added | Removed | Edited | Moved | Conflict,
}

// FR-045: search within the diff and filter by change type.
public static class DiffFilter
{
    public static IReadOnlyList<DiffRow> Apply(
        IReadOnlyList<DiffRow> rows, string? searchText = null, ChangeTypeFilter filter = ChangeTypeFilter.All)
    {
        if (string.IsNullOrEmpty(searchText) && filter == ChangeTypeFilter.All) return rows;

        return [.. rows.Where(row => Matches(row, searchText) && Matches(row, filter))];
    }

    public static IReadOnlyList<int> FindMatches(IReadOnlyList<DiffRow> rows, string searchText)
    {
        if (string.IsNullOrEmpty(searchText)) return [];

        var indices = new List<int>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (Matches(rows[i], searchText)) indices.Add(i);
        }

        return indices;
    }

    public static bool Matches(DiffRow row, string? searchText) =>
        string.IsNullOrEmpty(searchText)
        || row.Cells.Any(cell => cell.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase));

    public static bool Matches(DiffRow row, ChangeTypeFilter filter)
    {
        if (filter == ChangeTypeFilter.All) return true;
        if (row.Kind == DiffRowKind.Collapsed) return filter.HasFlag(ChangeTypeFilter.Unchanged);

        var flag = row.DisplayChangeType switch
        {
            ChangeType.Added => ChangeTypeFilter.Added,
            ChangeType.Removed => ChangeTypeFilter.Removed,
            ChangeType.Edited => ChangeTypeFilter.Edited,
            ChangeType.Moved or ChangeType.MovedEdited => ChangeTypeFilter.Moved,
            ChangeType.Conflict => ChangeTypeFilter.Conflict,
            _ => ChangeTypeFilter.Unchanged,
        };

        return filter.HasFlag(flag);
    }
}
