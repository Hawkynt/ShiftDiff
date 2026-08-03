namespace ShiftDiff.Core;

public static class UnifiedDiffBuilder
{
    private const string DefaultFileMode = "100644";

    /// <summary>
    /// FR-024: the same diff wrapped in git's extended header, so the result is a
    /// patch `git apply` accepts rather than a bare unified diff.
    /// </summary>
    public static UnifiedDiffFile BuildGit(
        IReadOnlyList<LineChange> changes, string oldPath, string newPath, int contextLines = 3)
    {
        var file = Build(changes, oldPath, newPath, contextLines);
        var oldName = GitPath(oldPath);
        var newName = GitPath(newPath);

        // An empty file still loads as a single empty line (TextFileLoader), so
        // "has no content" is what decides creation/deletion, not "has no lines".
        var oldIsEmpty = IsEmptySide(changes, isOld: true);
        var newIsEmpty = IsEmptySide(changes, isOld: false);
        var creation = (oldIsEmpty, newIsEmpty) switch
        {
            (true, false) => new GitFileCreationMode(GitFileCreationKind.NewFile, DefaultFileMode),
            (false, true) => new GitFileCreationMode(GitFileCreationKind.DeletedFile, DefaultFileMode),
            _ => null,
        };

        var rename = oldName == newName
            ? null
            : new GitRenameCopyMetadata(GitRenameCopyKind.Rename, oldName, newName);

        return file with
        {
            GitHeader = new GitExtendedHeader(
                new GitDiffHeader(oldName, newName), null, creation, null, rename, null),
        };
    }

    // Git paths are repo-relative and always use forward slashes.
    private static string GitPath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static bool IsEmptySide(IReadOnlyList<LineChange> changes, bool isOld) =>
        changes
            .Where(change => (isOld ? change.OldIndex : change.NewIndex) is not null)
            .All(change => string.IsNullOrEmpty(isOld ? change.OldLine : change.NewLine));

    public static UnifiedDiffFile Build(
        IReadOnlyList<LineChange> changes, string oldPath, string newPath, int contextLines = 3)
    {
        var header = new UnifiedDiffFileHeader(oldPath, newPath);

        var firstChangeIndex = -1;
        var lastChangeIndex = -1;
        for (var i = 0; i < changes.Count; i++)
        {
            if (changes[i].ChangeType == ChangeType.Unchanged)
            {
                continue;
            }

            if (firstChangeIndex == -1)
            {
                firstChangeIndex = i;
            }

            lastChangeIndex = i;
        }

        if (firstChangeIndex == -1)
        {
            return new UnifiedDiffFile(header, Array.Empty<UnifiedDiffHunk>());
        }

        var spanStart = Math.Max(0, firstChangeIndex - contextLines);
        var spanEnd = Math.Min(changes.Count - 1, lastChangeIndex + contextLines);

        var oldStart = 1 + CountOldLines(changes, 0, spanStart);
        var newStart = 1 + CountNewLines(changes, 0, spanStart);
        var oldCount = CountOldLines(changes, spanStart, spanEnd + 1);
        var newCount = CountNewLines(changes, spanStart, spanEnd + 1);

        var lines = new List<UnifiedDiffLine>();
        for (var i = spanStart; i <= spanEnd; i++)
        {
            var change = changes[i];
            switch (change.ChangeType)
            {
                case ChangeType.Unchanged:
                    lines.Add(new UnifiedDiffLine(UnifiedDiffLineKind.Context, change.OldLine!));
                    break;
                case ChangeType.Removed:
                    lines.Add(new UnifiedDiffLine(UnifiedDiffLineKind.Removed, change.OldLine!));
                    break;
                case ChangeType.Added:
                    lines.Add(new UnifiedDiffLine(UnifiedDiffLineKind.Added, change.NewLine!));
                    break;
                case ChangeType.Edited:
                    lines.Add(new UnifiedDiffLine(UnifiedDiffLineKind.Removed, change.OldLine!));
                    lines.Add(new UnifiedDiffLine(UnifiedDiffLineKind.Added, change.NewLine!));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(changes), change.ChangeType, "Unified diff building only supports Unchanged/Edited/Added/Removed line changes.");
            }
        }

        var hunk = new UnifiedDiffHunk(new UnifiedDiffHunkHeader(oldStart, oldCount, newStart, newCount), lines);

        return new UnifiedDiffFile(header, new[] { hunk });
    }

    private static int CountOldLines(IReadOnlyList<LineChange> changes, int start, int endExclusive)
    {
        var count = 0;
        for (var i = start; i < endExclusive; i++)
        {
            if (changes[i].ChangeType is ChangeType.Unchanged or ChangeType.Removed or ChangeType.Edited)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountNewLines(IReadOnlyList<LineChange> changes, int start, int endExclusive)
    {
        var count = 0;
        for (var i = start; i < endExclusive; i++)
        {
            if (changes[i].ChangeType is ChangeType.Unchanged or ChangeType.Added or ChangeType.Edited)
            {
                count++;
            }
        }

        return count;
    }
}
