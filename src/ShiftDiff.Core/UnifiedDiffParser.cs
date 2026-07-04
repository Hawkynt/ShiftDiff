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

public sealed record UnifiedDiffFile(UnifiedDiffFileHeader Header, IReadOnlyList<UnifiedDiffHunk> Hunks, GitExtendedHeader? GitHeader = null);

public sealed record UnifiedDiffPatch(IReadOnlyList<UnifiedDiffFile> Files);

public sealed record GitFileModeChange(string OldMode, string NewMode);

public enum GitFileCreationKind { NewFile, DeletedFile }

public sealed record GitFileCreationMode(GitFileCreationKind Kind, string Mode);

public enum GitSimilarityKind { Similarity, Dissimilarity }

public sealed record GitSimilarityIndex(GitSimilarityKind Kind, int Percentage);

public enum GitRenameCopyKind { Rename, Copy }

public sealed record GitRenameCopyMetadata(GitRenameCopyKind Kind, string SourcePath, string TargetPath);

public sealed record GitIndexHash(string OldHash, string NewHash, string? Mode);

public sealed record GitDiffHeader(string OldPath, string NewPath);

public sealed record GitExtendedHeader(
    GitDiffHeader Header,
    GitFileModeChange? ModeChange,
    GitFileCreationMode? CreationMode,
    GitSimilarityIndex? SimilarityIndex,
    GitRenameCopyMetadata? RenameCopyMetadata,
    GitIndexHash? IndexHash);

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

    public static GitFileCreationMode ParseFileCreationMode(string line)
    {
        if (line.StartsWith("new file mode ", StringComparison.Ordinal))
        {
            return new GitFileCreationMode(GitFileCreationKind.NewFile, line[14..]);
        }

        if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
        {
            return new GitFileCreationMode(GitFileCreationKind.DeletedFile, line[18..]);
        }

        throw new FormatException("The line is not a git file creation or deletion mode header.");
    }

    public static GitSimilarityIndex ParseSimilarityIndex(string line)
    {
        GitSimilarityKind kind;
        string remainder;
        if (line.StartsWith("similarity index ", StringComparison.Ordinal))
        {
            kind = GitSimilarityKind.Similarity;
            remainder = line[17..];
        }
        else if (line.StartsWith("dissimilarity index ", StringComparison.Ordinal))
        {
            kind = GitSimilarityKind.Dissimilarity;
            remainder = line[20..];
        }
        else
        {
            throw new FormatException("The line is not a git similarity index header.");
        }

        if (!remainder.EndsWith('%'))
        {
            throw new FormatException("The similarity index line has no percentage sign.");
        }

        return new GitSimilarityIndex(kind, int.Parse(remainder[..^1]));
    }

    public static GitRenameCopyMetadata ParseRenameCopyMetadata(string fromLine, string toLine)
    {
        GitRenameCopyKind kind;
        string sourcePath;
        if (fromLine.StartsWith("rename from ", StringComparison.Ordinal))
        {
            kind = GitRenameCopyKind.Rename;
            sourcePath = fromLine[12..];
        }
        else if (fromLine.StartsWith("copy from ", StringComparison.Ordinal))
        {
            kind = GitRenameCopyKind.Copy;
            sourcePath = fromLine[10..];
        }
        else
        {
            throw new FormatException("The from line is not a git rename or copy header.");
        }

        var toPrefix = kind == GitRenameCopyKind.Rename ? "rename to " : "copy to ";
        if (!toLine.StartsWith(toPrefix, StringComparison.Ordinal))
        {
            throw new FormatException("The to line does not match the from line's rename/copy kind.");
        }

        return new GitRenameCopyMetadata(kind, sourcePath, toLine[toPrefix.Length..]);
    }

    public static GitIndexHash ParseIndexHash(string line)
    {
        if (!line.StartsWith("index ", StringComparison.Ordinal))
        {
            throw new FormatException("The line is not a git index hash header.");
        }

        var rest = line[6..];
        var dotDotIndex = rest.IndexOf("..", StringComparison.Ordinal);
        if (dotDotIndex < 0)
        {
            throw new FormatException("The index hash line has no \"..\" separator.");
        }

        var oldHash = rest[..dotDotIndex];
        var afterDots = rest[(dotDotIndex + 2)..];
        var spaceIndex = afterDots.IndexOf(' ');
        return spaceIndex >= 0
            ? new GitIndexHash(oldHash, afterDots[..spaceIndex], afterDots[(spaceIndex + 1)..])
            : new GitIndexHash(oldHash, afterDots, null);
    }

    public static GitDiffHeader ParseGitDiffHeader(string line)
    {
        const string prefix = "diff --git a/";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FormatException("The line is not a git diff header.");
        }

        var rest = line[prefix.Length..];
        var firstSplitIndex = -1;
        var searchStart = 0;
        while (true)
        {
            var candidateIndex = rest.IndexOf(" b/", searchStart, StringComparison.Ordinal);
            if (candidateIndex < 0)
            {
                break;
            }

            if (firstSplitIndex < 0)
            {
                firstSplitIndex = candidateIndex;
            }

            var oldPath = rest[..candidateIndex];
            var newPath = rest[(candidateIndex + 3)..];
            if (oldPath == newPath)
            {
                return new GitDiffHeader(oldPath, newPath);
            }

            searchStart = candidateIndex + 1;
        }

        if (firstSplitIndex < 0)
        {
            throw new FormatException("The git diff header line has no \" b/\" separator.");
        }

        return new GitDiffHeader(rest[..firstSplitIndex], rest[(firstSplitIndex + 3)..]);
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

        GitExtendedHeader? gitHeader = null;
        var index = 0;
        if (lines[0].StartsWith("diff --git ", StringComparison.Ordinal))
        {
            var diffHeader = ParseGitDiffHeader(lines[0]);
            GitFileModeChange? modeChange = null;
            GitFileCreationMode? creationMode = null;
            GitSimilarityIndex? similarityIndex = null;
            GitRenameCopyMetadata? renameCopyMetadata = null;
            GitIndexHash? indexHash = null;
            index = 1;
            while (index < lines.Count)
            {
                var line = lines[index];
                if (line.StartsWith("old mode ", StringComparison.Ordinal))
                {
                    modeChange = ParseFileModeChange(line, lines[index + 1]);
                    index += 2;
                }
                else if (line.StartsWith("new file mode ", StringComparison.Ordinal) ||
                         line.StartsWith("deleted file mode ", StringComparison.Ordinal))
                {
                    creationMode = ParseFileCreationMode(line);
                    index += 1;
                }
                else if (line.StartsWith("similarity index ", StringComparison.Ordinal) ||
                         line.StartsWith("dissimilarity index ", StringComparison.Ordinal))
                {
                    similarityIndex = ParseSimilarityIndex(line);
                    index += 1;
                }
                else if (line.StartsWith("rename from ", StringComparison.Ordinal) ||
                         line.StartsWith("copy from ", StringComparison.Ordinal))
                {
                    renameCopyMetadata = ParseRenameCopyMetadata(line, lines[index + 1]);
                    index += 2;
                }
                else if (line.StartsWith("index ", StringComparison.Ordinal))
                {
                    indexHash = ParseIndexHash(line);
                    index += 1;
                }
                else
                {
                    break;
                }
            }

            gitHeader = new GitExtendedHeader(diffHeader, modeChange, creationMode, similarityIndex, renameCopyMetadata, indexHash);
        }

        UnifiedDiffFileHeader header;
        if (index < lines.Count && lines[index].StartsWith("--- ", StringComparison.Ordinal))
        {
            header = ParseFileHeader(lines[index], lines[index + 1]);
            index += 2;
        }
        else if (gitHeader is not null)
        {
            // A pure git rename/copy/mode-change block carries no "--- "/"+++ " pair
            // and no content hunks — the paths come from the git metadata instead.
            var renameCopyMetadata = gitHeader.RenameCopyMetadata;
            header = new UnifiedDiffFileHeader(
                renameCopyMetadata?.SourcePath ?? gitHeader.Header.OldPath,
                renameCopyMetadata?.TargetPath ?? gitHeader.Header.NewPath);
        }
        else
        {
            throw new FormatException("Expected a file header line.");
        }

        var hunks = new List<UnifiedDiffHunk>();
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

        return new UnifiedDiffFile(header, hunks, gitHeader);
    }

    public static UnifiedDiffPatch ParsePatch(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return new UnifiedDiffPatch(Array.Empty<UnifiedDiffFile>());
        }

        if (!lines[0].StartsWith("--- ", StringComparison.Ordinal) &&
            !lines[0].StartsWith("diff --git ", StringComparison.Ordinal))
        {
            throw new FormatException("Expected a file header line.");
        }

        var files = new List<UnifiedDiffFile>();
        var index = 0;
        while (index < lines.Count)
        {
            var blockStart = index;
            index++;
            if (lines[blockStart].StartsWith("diff --git ", StringComparison.Ordinal))
            {
                // Skip past this block's own "--- "/"+++ " pair before scanning for the
                // next block's boundary, or it would be mistaken for a new file's start.
                // A pure rename/mode-change block has no such pair at all — stop at the
                // next "diff --git " line instead of scanning into a later block's pair.
                while (index < lines.Count &&
                       !lines[index].StartsWith("--- ", StringComparison.Ordinal) &&
                       !lines[index].StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    index++;
                }

                if (index < lines.Count && lines[index].StartsWith("--- ", StringComparison.Ordinal))
                {
                    index += 2;
                }
            }

            while (index < lines.Count &&
                   !lines[index].StartsWith("--- ", StringComparison.Ordinal) &&
                   !lines[index].StartsWith("diff --git ", StringComparison.Ordinal))
            {
                index++;
            }

            files.Add(ParseFile(lines.Skip(blockStart).Take(index - blockStart).ToList()));
        }

        return new UnifiedDiffPatch(files);
    }
}
