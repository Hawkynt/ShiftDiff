namespace ShiftDiff.Core;

public static class FolderRenameDetector
{
    private const double DefaultThreshold = 0.5;

    public static FolderEntryChange[] Detect(
        IReadOnlyList<FolderEntryChange> changes,
        IReadOnlyDictionary<string, byte[]> baseFiles,
        IReadOnlyDictionary<string, byte[]> targetFiles,
        double threshold = DefaultThreshold)
    {
        var removed = changes.Where(c => c.ChangeType == FolderChangeType.Removed).ToList();
        var matchedRemoved = new HashSet<string>();
        var renamedFromByAddedPath = new Dictionary<string, string>();

        foreach (var added in changes.Where(c => c.ChangeType == FolderChangeType.Added))
        {
            var addedLines = TextFileLoader.Load(targetFiles[added.RelativePath]).Lines;
            var candidates = removed
                .Where(r => !matchedRemoved.Contains(r.RelativePath))
                .Where(r => Similarity(TextFileLoader.Load(baseFiles[r.RelativePath]).Lines, addedLines) >= threshold)
                .ToList();

            if (candidates.Count == 1)
            {
                matchedRemoved.Add(candidates[0].RelativePath);
                renamedFromByAddedPath[added.RelativePath] = candidates[0].RelativePath;
            }
        }

        if (renamedFromByAddedPath.Count == 0)
        {
            return changes.ToArray();
        }

        return changes
            .Where(c => !(c.ChangeType == FolderChangeType.Removed && matchedRemoved.Contains(c.RelativePath)))
            .Select(c => renamedFromByAddedPath.TryGetValue(c.RelativePath, out var movedFrom)
                ? c with { ChangeType = FolderChangeType.MovedEdited, MovedFrom = movedFrom }
                : c)
            .ToArray();
    }

    // Whole-file spans can differ in line count (unlike BlockSimilarityScorer's
    // matched-block callers), so only offset-independent metrics are composed here —
    // ExactHashOverlap/NormalizedHashOverlap assume equal-length paired offsets and
    // would index out of range.
    private static double Similarity(string[] oldLines, string[] newLines)
    {
        if (oldLines.Length == 0 || newLines.Length == 0)
        {
            return 0.0;
        }

        var candidate = new BlockCandidate(0, oldLines.Length - 1, 0, newLines.Length - 1);
        return (BlockSimilarityScorer.TokenShingleSimilarity(candidate, oldLines, newLines)
            + BlockSimilarityScorer.SimHashSimilarity(candidate, oldLines, newLines)
            + BlockSimilarityScorer.BlockSizeRatio(candidate, oldLines, newLines)) / 3.0;
    }
}
