namespace ShiftDiff.Core;

public static class BlockClassifier
{
    public static BlockMatch[] Classify(
        BlockCandidate[] candidates,
        string[] oldLines,
        string[] newLines,
        DetectionMode mode = DetectionMode.Balanced,
        int minBlockSize = 1,
        int minTokenCount = 0,
        int maxDuplicateAnchorFrequency = int.MaxValue,
        double? pureMoveThreshold = null)
    {
        var matches = new BlockMatch[candidates.Length];
        var threshold = DetectionModeThresholds.MovedConfidenceThreshold(mode);
        var duplicateCounts = AnchorDetector.DuplicateCounts(oldLines);

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            var score = BlockSimilarityScorer.CombinedScore(candidate, oldLines, newLines);
            var blockSize = candidate.OldEnd - candidate.OldStart + 1;
            var tokenCount = BlockSimilarityScorer.TokenCount(candidate, oldLines, newLines);
            var maxDuplicateCount = 0;
            for (var offset = 0; offset < blockSize; offset++)
            {
                var count = duplicateCounts[candidate.OldStart + offset];
                if (count > maxDuplicateCount) maxDuplicateCount = count;
            }

            var passesFilters = score >= threshold && blockSize >= minBlockSize && tokenCount >= minTokenCount && maxDuplicateCount <= maxDuplicateAnchorFrequency;
            var matchType = passesFilters
                ? (pureMoveThreshold is null || score >= pureMoveThreshold.Value ? ChangeType.Moved : ChangeType.MovedEdited)
                : ChangeType.Uncertain;
            // Confidence is score-derived only (FR-015), independent of the FR-016 mode
            // threshold — a block can be Uncertain under a strict mode yet still Certain.
            var confidence = ConfidenceClassifier.Classify(score);
            matches[index] = new BlockMatch(
                candidate.OldStart,
                candidate.OldEnd,
                candidate.NewStart,
                candidate.NewEnd,
                matchType,
                score,
                confidence);
        }

        return matches;
    }
}
