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
        // Precompute each removed file's lines once — the old per-candidate reload inside the outer
        // `added` loop re-parsed the same removed file's content once per still-unmatched `added`
        // entry (O(M*K) reloads for M added / K removed files instead of O(K)), the same redundant-
        // rescan shape found repeatedly elsewhere in this repo (BlockClassifier/BlockSimilarityScorer/
        // PatchApplier).
        var removedLinesByPath = removed.ToDictionary(r => r.RelativePath, r => TextFileLoader.Load(baseFiles[r.RelativePath]).Lines);
        // Precompute each removed file's token-shingle/SimHash fingerprint once too — Similarity used to
        // derive these fresh from the raw lines on every candidate pair, redoing the same O(fileSize)
        // tokenize/shingle/fingerprint work once per still-unmatched `added` file it was compared against.
        var removedFingerprintByPath = removedLinesByPath.ToDictionary(pair => pair.Key, pair => BlockSimilarityScorer.ComputeFileFingerprint(pair.Value));
        var matchedRemoved = new HashSet<string>();
        var renamedFromByAddedPath = new Dictionary<string, string>();

        foreach (var added in changes.Where(c => c.ChangeType == FolderChangeType.Added))
        {
            var addedLines = TextFileLoader.Load(targetFiles[added.RelativePath]).Lines;
            var addedFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(addedLines);
            var candidates = removed
                .Where(r => !matchedRemoved.Contains(r.RelativePath))
                .Where(r => Similarity(removedFingerprintByPath[r.RelativePath], addedFingerprint, removedLinesByPath[r.RelativePath], addedLines) >= threshold)
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
    private static double Similarity(BlockSimilarityScorer.FileFingerprint oldFingerprint, BlockSimilarityScorer.FileFingerprint newFingerprint, string[] oldLines, string[] newLines)
    {
        if (oldLines.Length == 0 || newLines.Length == 0)
        {
            return 0.0;
        }

        var candidate = new BlockCandidate(0, oldLines.Length - 1, 0, newLines.Length - 1);
        return (BlockSimilarityScorer.TokenShingleSimilarityFromFingerprint(oldFingerprint, newFingerprint)
            + BlockSimilarityScorer.SimHashSimilarityFromFingerprint(oldFingerprint, newFingerprint)
            + BlockSimilarityScorer.BlockSizeRatio(candidate, oldLines, newLines)) / 3.0;
    }
}
