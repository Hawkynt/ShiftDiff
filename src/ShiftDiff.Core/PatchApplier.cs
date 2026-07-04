namespace ShiftDiff.Core;

public sealed class PatchApplicationException : Exception
{
    public PatchApplicationException(string message) : base(message)
    {
    }
}

public static class PatchApplier
{
    public static IReadOnlyList<string> ApplyHunkExact(IReadOnlyList<string> sourceLines, UnifiedDiffHunk hunk)
    {
        var oldStartIndex = hunk.Header.OldStart - 1;
        var oldLines = hunk.Lines
            .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Removed)
            .ToList();

        for (var i = 0; i < oldLines.Count; i++)
        {
            var sourceIndex = oldStartIndex + i;
            if (sourceIndex >= sourceLines.Count || sourceLines[sourceIndex] != oldLines[i].Content)
            {
                throw new PatchApplicationException(
                    $"Hunk context/removed content does not match source at line {sourceIndex + 1}.");
            }
        }

        var newLines = hunk.Lines
            .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Added)
            .Select(line => line.Content);

        var result = new List<string>(sourceLines.Count - oldLines.Count + hunk.Header.NewCount);
        result.AddRange(sourceLines.Take(oldStartIndex));
        result.AddRange(newLines);
        result.AddRange(sourceLines.Skip(oldStartIndex + hunk.Header.OldCount));
        return result;
    }

    public static IReadOnlyList<string> ApplyFileExact(IReadOnlyList<string> sourceLines, UnifiedDiffFile file)
    {
        var result = sourceLines;
        foreach (var hunk in file.Hunks.Reverse())
        {
            result = ApplyHunkExact(result, hunk);
        }

        return result;
    }
}
