using System.Text.RegularExpressions;

namespace ShiftDiff.Core;

public sealed record UnifiedDiffHunkHeader(int OldStart, int OldCount, int NewStart, int NewCount);

public sealed record UnifiedDiffFileHeader(string SourcePath, string TargetPath);

public enum UnifiedDiffLineKind { Context, Added, Removed }

public sealed record UnifiedDiffLine(UnifiedDiffLineKind Kind, string Content);

public sealed record UnifiedDiffHunk(UnifiedDiffHunkHeader Header, IReadOnlyList<UnifiedDiffLine> Lines);

public static class UnifiedDiffParser
{
    private static readonly Regex HunkHeaderPattern = new(
        @"^@@ -(?<oldStart>\d+)(?:,(?<oldCount>\d+))? \+(?<newStart>\d+)(?:,(?<newCount>\d+))? @@(?: .*)?$",
        RegexOptions.Compiled);

    public static UnifiedDiffFileHeader ParseFileHeader(string oldLine, string newLine)
    {
        if (!oldLine.StartsWith("--- ", StringComparison.Ordinal))
        {
            throw new FormatException("The old line is not a unified diff file header.");
        }

        if (!newLine.StartsWith("+++ ", StringComparison.Ordinal))
        {
            throw new FormatException("The new line is not a unified diff file header.");
        }

        return new UnifiedDiffFileHeader(
            ExtractPath(oldLine),
            ExtractPath(newLine));
    }

    private static string ExtractPath(string line)
    {
        var path = line[4..];
        var tabIndex = path.IndexOf('\t');
        return tabIndex >= 0 ? path[..tabIndex] : path;
    }

    public static UnifiedDiffHunkHeader ParseHunkHeader(string line)
    {
        var match = HunkHeaderPattern.Match(line);
        if (!match.Success)
        {
            throw new FormatException("The line is not a unified diff hunk header.");
        }

        return new UnifiedDiffHunkHeader(
            int.Parse(match.Groups["oldStart"].Value),
            ParseCount(match.Groups["oldCount"]),
            int.Parse(match.Groups["newStart"].Value),
            ParseCount(match.Groups["newCount"]));
    }

    private static int ParseCount(Group group)
    {
        if (!group.Success)
        {
            return 1;
        }

        return int.Parse(group.Value);
    }

    public static UnifiedDiffLine ParseLine(string line)
    {
        if (line.Length == 0)
        {
            throw new FormatException("The line is empty.");
        }

        var kind = line[0] switch
        {
            '+' => UnifiedDiffLineKind.Added,
            '-' => UnifiedDiffLineKind.Removed,
            ' ' => UnifiedDiffLineKind.Context,
            _ => throw new FormatException("The line does not start with a recognized unified diff marker."),
        };

        return new UnifiedDiffLine(kind, line[1..]);
    }

    public static UnifiedDiffHunk ParseHunk(string headerLine, IReadOnlyList<string> bodyLines)
    {
        var header = ParseHunkHeader(headerLine);
        var lines = bodyLines.Select(ParseLine).ToList();
        return new UnifiedDiffHunk(header, lines);
    }
}
