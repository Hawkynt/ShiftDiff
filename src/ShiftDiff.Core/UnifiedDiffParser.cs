using System.Text.RegularExpressions;

namespace ShiftDiff.Core;

public sealed record UnifiedDiffHunkHeader(int OldStart, int OldCount, int NewStart, int NewCount);

public sealed record UnifiedDiffFileHeader(string SourcePath, string TargetPath);

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
}
