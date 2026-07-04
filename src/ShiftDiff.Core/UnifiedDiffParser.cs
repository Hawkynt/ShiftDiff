using System.Text.RegularExpressions;

namespace ShiftDiff.Core;

public sealed record UnifiedDiffHunkHeader(int OldStart, int OldCount, int NewStart, int NewCount);

public static class UnifiedDiffParser
{
    private static readonly Regex HunkHeaderPattern = new(
        @"^@@ -(?<oldStart>\d+)(?:,(?<oldCount>\d+))? \+(?<newStart>\d+)(?:,(?<newCount>\d+))? @@(?: .*)?$",
        RegexOptions.Compiled);

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
