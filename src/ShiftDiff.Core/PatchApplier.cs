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

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ApplyPatchExact(
        UnifiedDiffPatch patch, IReadOnlyDictionary<string, IReadOnlyList<string>> sourcesBySourcePath)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var file in patch.Files)
        {
            var sourcePath = file.Header.SourcePath;
            if (!sourcesBySourcePath.TryGetValue(sourcePath, out var sourceLines))
            {
                throw new PatchApplicationException($"No source provided for path '{sourcePath}'.");
            }

            result[file.Header.TargetPath] = ApplyFileExact(sourceLines, file);
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

        var match = FindClosestMatch(sourceLines, oldLines, recordedStartIndex);
        if (match is null)
        {
            throw new PatchApplicationException(
                "Hunk context/removed content was not found anywhere in the source.");
        }

        var matchStartIndex = match.Value.Index;
        var confidence = match.Value.Kind == MatchKind.Exact && matchStartIndex == recordedStartIndex
            ? PatchApplicationConfidence.Exact
            : PatchApplicationConfidence.High;

        // A leading/trailing Context line matched only via drift tolerance was
        // never actually verified against the source at this position — the
        // hunk's recorded content for that line is spliced through unchanged
        // elsewhere, so here we must keep the source's real (unverified) line
        // instead of overwriting it with the hunk's recorded Context content.
        var linesToInsert = newLines;
        if (match.Value.Kind == MatchKind.LeadingDrift)
        {
            linesToInsert = new List<string>(newLines) { [0] = sourceLines[matchStartIndex] };
        }
        else if (match.Value.Kind == MatchKind.TrailingDrift)
        {
            linesToInsert = new List<string>(newLines);
            linesToInsert[^1] = sourceLines[matchStartIndex + oldLines.Count - 1];
        }

        var result = new List<string>(sourceLines.Count - oldLines.Count + linesToInsert.Count);
        result.AddRange(sourceLines.Take(matchStartIndex));
        result.AddRange(linesToInsert);
        result.AddRange(sourceLines.Skip(matchStartIndex + oldLines.Count));
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

    private enum MatchKind
    {
        Exact,
        LeadingDrift,
        TrailingDrift,
    }

    private readonly record struct FuzzyMatch(int Index, MatchKind Kind);

    private static FuzzyMatch? FindClosestMatch(
        IReadOnlyList<string> sourceLines, IReadOnlyList<UnifiedDiffLine> oldLines, int preferredIndex)
    {
        var lastPossibleStart = sourceLines.Count - oldLines.Count;
        if (lastPossibleStart < 0)
        {
            return null;
        }

        var leadingContextRun = CountLeadingContextRun(oldLines);
        var trailingContextRun = CountTrailingContextRun(oldLines);

        FuzzyMatch? best = null;
        var bestDistance = int.MaxValue;
        for (var candidate = 0; candidate <= lastPossibleStart; candidate++)
        {
            var kind = MatchAt(sourceLines, oldLines, candidate, leadingContextRun, trailingContextRun);
            if (kind is null)
            {
                continue;
            }

            var distance = Math.Abs(candidate - preferredIndex);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = new FuzzyMatch(candidate, kind.Value);
            }
        }

        return best;
    }

    // Counts Context-kind lines before the first Removed-kind line — the
    // leading edge fuzz tolerance may relax at most this many lines (fuzz
    // level 1 here means at most the single outermost one).
    private static int CountLeadingContextRun(IReadOnlyList<UnifiedDiffLine> oldLines)
    {
        var count = 0;
        while (count < oldLines.Count && oldLines[count].Kind == UnifiedDiffLineKind.Context)
        {
            count++;
        }

        return count;
    }

    // Counts Context-kind lines after the last Removed-kind line — the
    // trailing counterpart of CountLeadingContextRun.
    private static int CountTrailingContextRun(IReadOnlyList<UnifiedDiffLine> oldLines)
    {
        var count = 0;
        while (count < oldLines.Count && oldLines[oldLines.Count - 1 - count].Kind == UnifiedDiffLineKind.Context)
        {
            count++;
        }

        return count;
    }

    private static MatchKind? MatchAt(
        IReadOnlyList<string> sourceLines, IReadOnlyList<UnifiedDiffLine> oldLines, int start,
        int leadingContextRun, int trailingContextRun)
    {
        if (MatchesAtRange(sourceLines, oldLines, start, 0, oldLines.Count))
        {
            return MatchKind.Exact;
        }

        // Fuzz level 1: relax at most the single outermost Context line on
        // one edge at a time (never both edges together, never a Removed
        // line — Removed lines are the actual change and must always match).
        if (leadingContextRun > 0 && MatchesAtRange(sourceLines, oldLines, start, 1, oldLines.Count))
        {
            return MatchKind.LeadingDrift;
        }

        if (trailingContextRun > 0 && MatchesAtRange(sourceLines, oldLines, start, 0, oldLines.Count - 1))
        {
            return MatchKind.TrailingDrift;
        }

        return null;
    }

    private static bool MatchesAtRange(
        IReadOnlyList<string> sourceLines, IReadOnlyList<UnifiedDiffLine> oldLines, int start, int fromInclusive, int toExclusive)
    {
        for (var i = fromInclusive; i < toExclusive; i++)
        {
            if (sourceLines[start + i] != oldLines[i].Content)
            {
                return false;
            }
        }

        return true;
    }
}
