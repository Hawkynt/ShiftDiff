using System.Text.RegularExpressions;

namespace ShiftDiff.Core;

public sealed record UnifiedDiffHunkHeader(int OldStart, int OldCount, int NewStart, int NewCount);

public sealed record UnifiedDiffFileHeader(
    string SourcePath,
    string TargetPath,
    string? SourceRevision = null,
    string? TargetRevision = null);

public enum UnifiedDiffLineKind { Context, Added, Removed }

public sealed record UnifiedDiffLine(UnifiedDiffLineKind Kind, string Content);

public sealed record UnifiedDiffHunk(UnifiedDiffHunkHeader Header, IReadOnlyList<UnifiedDiffLine> Lines);

public sealed record UnifiedDiffFile(UnifiedDiffFileHeader Header, IReadOnlyList<UnifiedDiffHunk> Hunks);

public sealed record UnifiedDiffPatch(IReadOnlyList<UnifiedDiffFile> Files);

public sealed record GitFileModeChange(string OldMode, string NewMode);

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

        var (sourcePath, sourceRevision) = ExtractPathAndRevision(oldLine);
        var (targetPath, targetRevision) = ExtractPathAndRevision(newLine);

        return new UnifiedDiffFileHeader(sourcePath, targetPath, sourceRevision, targetRevision);
    }

    private static (string Path, string? Revision) ExtractPathAndRevision(string line)
    {
        var rest = line[4..];
        var tabIndex = rest.IndexOf('\t');
        return tabIndex >= 0
            ? (rest[..tabIndex], rest[(tabIndex + 1)..])
            : (rest, null);
    }

    public static GitFileModeChange ParseFileModeChange(string oldModeLine, string newModeLine)
    {
        if (!oldModeLine.StartsWith("old mode ", StringComparison.Ordinal))
        {
            throw new FormatException("The old line is not a git file mode change header.");
        }

        if (!newModeLine.StartsWith("new mode ", StringComparison.Ordinal))
        {
            throw new FormatException("The new line is not a git file mode change header.");
        }

        return new GitFileModeChange(oldModeLine[9..], newModeLine[9..]);
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

    public static UnifiedDiffFile ParseFile(IReadOnlyList<string> lines)
    {
        if (lines.Count < 2)
        {
            throw new FormatException("A unified diff file needs at least two header lines.");
        }

        var header = ParseFileHeader(lines[0], lines[1]);

        var hunks = new List<UnifiedDiffHunk>();
        var index = 2;
        while (index < lines.Count)
        {
            if (!lines[index].StartsWith("@@", StringComparison.Ordinal))
            {
                throw new FormatException("Expected a hunk header line.");
            }

            var headerLine = lines[index];
            index++;

            var bodyStart = index;
            while (index < lines.Count && !lines[index].StartsWith("@@", StringComparison.Ordinal))
            {
                index++;
            }

            hunks.Add(ParseHunk(headerLine, lines.Skip(bodyStart).Take(index - bodyStart).ToList()));
        }

        return new UnifiedDiffFile(header, hunks);
    }

    public static UnifiedDiffPatch ParsePatch(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return new UnifiedDiffPatch(Array.Empty<UnifiedDiffFile>());
        }

        if (!lines[0].StartsWith("--- ", StringComparison.Ordinal))
        {
            throw new FormatException("Expected a file header line.");
        }

        var files = new List<UnifiedDiffFile>();
        var index = 0;
        while (index < lines.Count)
        {
            var blockStart = index;
            index++;
            while (index < lines.Count && !lines[index].StartsWith("--- ", StringComparison.Ordinal))
            {
                index++;
            }

            files.Add(ParseFile(lines.Skip(blockStart).Take(index - blockStart).ToList()));
        }

        return new UnifiedDiffPatch(files);
    }
}
