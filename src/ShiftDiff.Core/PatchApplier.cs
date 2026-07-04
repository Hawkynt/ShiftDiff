namespace ShiftDiff.Core;

public sealed class PatchApplicationException : Exception
{
    public PatchApplicationException(string message) : base(message)
    {
    }
}

public enum PatchApplicationConfidence
{
    Exact,
    High
}

public sealed record PatchApplicationResult(IReadOnlyList<string> Lines, PatchApplicationConfidence Confidence);

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

    public static PatchApplicationResult ApplyHunkFuzzy(IReadOnlyList<string> sourceLines, UnifiedDiffHunk hunk)
    {
        var oldLines = hunk.Lines
            .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Removed)
            .ToList();
        var newLines = hunk.Lines
            .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Added)
            .Select(line => line.Content)
            .ToList();
        var recordedStartIndex = hunk.Header.OldStart - 1;

        if (oldLines.Count == 0)
        {
            var insertion = new List<string>(sourceLines.Count + newLines.Count);
            insertion.AddRange(sourceLines.Take(recordedStartIndex));
            insertion.AddRange(newLines);
            insertion.AddRange(sourceLines.Skip(recordedStartIndex));
            return new PatchApplicationResult(insertion, PatchApplicationConfidence.Exact);
        }

        var matchStartIndex = FindClosestMatch(sourceLines, oldLines, recordedStartIndex);
        if (matchStartIndex is null)
        {
            throw new PatchApplicationException(
                "Hunk context/removed content was not found anywhere in the source.");
        }

        var confidence = matchStartIndex.Value == recordedStartIndex
            ? PatchApplicationConfidence.Exact
            : PatchApplicationConfidence.High;

        var result = new List<string>(sourceLines.Count - oldLines.Count + newLines.Count);
        result.AddRange(sourceLines.Take(matchStartIndex.Value));
        result.AddRange(newLines);
        result.AddRange(sourceLines.Skip(matchStartIndex.Value + oldLines.Count));
        return new PatchApplicationResult(result, confidence);
    }

    public static PatchApplicationResult ApplyFileFuzzy(IReadOnlyList<string> sourceLines, UnifiedDiffFile file)
    {
        IReadOnlyList<string> lines = sourceLines;
        var confidence = PatchApplicationConfidence.Exact;
        foreach (var hunk in file.Hunks.Reverse())
        {
            var hunkResult = ApplyHunkFuzzy(lines, hunk);
            lines = hunkResult.Lines;
            if (hunkResult.Confidence == PatchApplicationConfidence.High)
            {
                confidence = PatchApplicationConfidence.High;
            }
        }

        return new PatchApplicationResult(lines, confidence);
    }

    private static int? FindClosestMatch(
        IReadOnlyList<string> sourceLines, IReadOnlyList<UnifiedDiffLine> oldLines, int preferredIndex)
    {
        var lastPossibleStart = sourceLines.Count - oldLines.Count;
        if (lastPossibleStart < 0)
        {
            return null;
        }

        int? bestIndex = null;
        var bestDistance = int.MaxValue;
        for (var candidate = 0; candidate <= lastPossibleStart; candidate++)
        {
            if (!MatchesAt(sourceLines, oldLines, candidate))
            {
                continue;
            }

            var distance = Math.Abs(candidate - preferredIndex);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = candidate;
            }
        }

        return bestIndex;
    }

    private static bool MatchesAt(IReadOnlyList<string> sourceLines, IReadOnlyList<UnifiedDiffLine> oldLines, int start)
    {
        for (var i = 0; i < oldLines.Count; i++)
        {
            if (sourceLines[start + i] != oldLines[i].Content)
            {
                return false;
            }
        }

        return true;
    }
}
