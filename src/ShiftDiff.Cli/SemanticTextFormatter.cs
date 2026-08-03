using System.Text;
using ShiftDiff.Core;

namespace ShiftDiff.Cli;

// The CLI's default rendering: unlike a plain unified diff it names the moved
// blocks the engine found (FR-011/FR-046) and shows intra-line token changes
// inside edited lines (FR-060) instead of a delete/add pair.
public static class SemanticTextFormatter
{
    public static IReadOnlyList<string> Format(
        string oldPath,
        string newPath,
        SourceLanguage language,
        FileComparisonResult comparison,
        bool useEmoji = false,
        int contextLines = 3)
    {
        var changes = comparison.Changes;

        // R-001: report the blocks that genuinely changed order, not every block
        // that slid down because lines were inserted above it.
        var movedBlocks = MoveRefiner.Refine(comparison.MovedBlocks);

        var lines = new List<string>
        {
            $"--- {oldPath}",
            $"+++ {newPath}",
            Summary(language, comparison, movedBlocks.Length),
        };

        foreach (var block in movedBlocks)
        {
            lines.Add(FormatBlock(block, useEmoji));
        }

        var hunks = HunkGrouper.Group(changes, contextLines);
        if (hunks.Length == 0) return lines;

        var oldWidth = Math.Max(4, changes.Max(change => change.OldIndex + 1 ?? 0).ToString().Length);
        var newWidth = Math.Max(4, changes.Max(change => change.NewIndex + 1 ?? 0).ToString().Length);

        foreach (var hunk in hunks)
        {
            lines.Add($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");
            for (var i = hunk.StartIndex; i <= hunk.EndIndex; i++)
            {
                lines.AddRange(FormatChange(changes[i], movedBlocks, useEmoji, oldWidth, newWidth));
            }
        }

        return lines;
    }

    public static string RenderTokens(IReadOnlyList<TokenChange> tokens)
    {
        var builder = new StringBuilder();
        foreach (var token in tokens)
        {
            switch (token.ChangeType)
            {
                case ChangeType.Added:
                    builder.Append("{+").Append(token.NewToken).Append("+}");
                    break;
                case ChangeType.Removed:
                    builder.Append("[-").Append(token.OldToken).Append("-]");
                    break;
                case ChangeType.Edited:
                    builder.Append("[-").Append(token.OldToken).Append("-]{+").Append(token.NewToken).Append("+}");
                    break;
                default:
                    builder.Append(token.NewToken ?? token.OldToken);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string Summary(SourceLanguage language, FileComparisonResult comparison, int movedBlockCount)
    {
        var added = comparison.Changes.Count(change => change.ChangeType == ChangeType.Added);
        var removed = comparison.Changes.Count(change => change.ChangeType == ChangeType.Removed);
        var edited = comparison.Changes.Count(change => change.ChangeType == ChangeType.Edited);
        var moved = movedBlockCount;
        return $"# {SourceLanguageDetector.GetDisplayName(language)} · {added} added · {removed} removed · {edited} edited · {moved} moved block(s)";
    }

    private static string FormatBlock(BlockMatch block, bool useEmoji) =>
        $"{ChangeMarker.For(block.MatchType, useEmoji)} {ChangeMarker.Label(block.MatchType)}: " +
        $"old {block.OldStart + 1}-{block.OldEnd + 1} -> new {block.NewStart + 1}-{block.NewEnd + 1} " +
        $"({ChangeMarker.Label(block.Confidence)}, {block.Score:P0})";

    private static IEnumerable<string> FormatChange(
        LineChange change, IReadOnlyList<BlockMatch> movedBlocks, bool useEmoji, int oldWidth, int newWidth)
    {
        var oldNumber = (change.OldIndex is { } oldIndex ? (oldIndex + 1).ToString() : string.Empty).PadLeft(oldWidth);
        var newNumber = (change.NewIndex is { } newIndex ? (newIndex + 1).ToString() : string.Empty).PadLeft(newWidth);

        if (change.ChangeType == ChangeType.Edited && change.TokenChanges is { Length: > 0 } tokens)
        {
            yield return $"{ChangeMarker.For(ChangeType.Edited, useEmoji)} {oldNumber} {newNumber} {RenderTokens(tokens)}";
            yield break;
        }

        if (change.ChangeType == ChangeType.Edited)
        {
            yield return $"{ChangeMarker.For(ChangeType.Removed, useEmoji)} {oldNumber} {new string(' ', newWidth)} {change.OldLine}";
            yield return $"{ChangeMarker.For(ChangeType.Added, useEmoji)} {new string(' ', oldWidth)} {newNumber} {change.NewLine}";
            yield break;
        }

        var type = change.ChangeType == ChangeType.Unchanged && IsInMovedBlock(change, movedBlocks)
            ? ChangeType.Moved
            : change.ChangeType;

        yield return $"{ChangeMarker.For(type, useEmoji)} {oldNumber} {newNumber} {change.NewLine ?? change.OldLine}";
    }

    private static bool IsInMovedBlock(LineChange change, IReadOnlyList<BlockMatch> blocks) =>
        blocks.Any(block =>
            (change.OldIndex is { } oldIndex && oldIndex >= block.OldStart && oldIndex <= block.OldEnd)
            || (change.NewIndex is { } newIndex && newIndex >= block.NewStart && newIndex <= block.NewEnd));
}
