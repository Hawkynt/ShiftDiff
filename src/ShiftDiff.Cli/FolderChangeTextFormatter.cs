using ShiftDiff.Core;

namespace ShiftDiff.Cli;

// FR-004 folder comparison, rendered as a flat change list with the same marker
// vocabulary the file diff uses.
public static class FolderChangeTextFormatter
{
    public static IReadOnlyList<string> Format(
        string basePath, string targetPath, IReadOnlyList<FolderEntryChange> changes, bool useEmoji = false)
    {
        var lines = new List<string>
        {
            $"--- {basePath}",
            $"+++ {targetPath}",
            $"# {changes.Count(c => c.ChangeType == FolderChangeType.Added)} added · " +
            $"{changes.Count(c => c.ChangeType == FolderChangeType.Removed)} removed · " +
            $"{changes.Count(c => c.ChangeType == FolderChangeType.Changed)} changed · " +
            $"{changes.Count(c => c.ChangeType is FolderChangeType.Moved or FolderChangeType.MovedEdited)} moved · " +
            $"{changes.Count(c => c.ChangeType == FolderChangeType.Copied)} copied · " +
            $"{changes.Count(c => c.ChangeType == FolderChangeType.Unchanged)} unchanged",
        };

        foreach (var change in changes)
        {
            if (change.ChangeType == FolderChangeType.Unchanged) continue;

            var marker = ChangeMarker.For(ToChangeType(change.ChangeType), useEmoji);
            var origin = change.MovedFrom is { } movedFrom
                ? $"  (from {movedFrom})"
                : change.CopiedFrom is { } copiedFrom ? $"  (copy of {copiedFrom})" : string.Empty;
            lines.Add($"{marker} {Label(change.ChangeType),-12} {change.RelativePath}{origin}");
        }

        return lines;
    }

    private static ChangeType ToChangeType(FolderChangeType type) => type switch
    {
        FolderChangeType.Added => ChangeType.Added,
        FolderChangeType.Removed => ChangeType.Removed,
        FolderChangeType.Changed => ChangeType.Edited,
        FolderChangeType.Moved => ChangeType.Moved,
        FolderChangeType.MovedEdited => ChangeType.MovedEdited,
        FolderChangeType.Copied => ChangeType.Split,
        _ => ChangeType.Unchanged,
    };

    private static string Label(FolderChangeType type) => type switch
    {
        FolderChangeType.MovedEdited => "moved+edited",
        FolderChangeType.Copied => "copied",
        _ => type.ToString().ToLowerInvariant(),
    };
}
