namespace ShiftDiff.Core;

public static class BlockClassifier
{
    public static BlockMatch[] Classify(
        BlockCandidate[] candidates,
        string[] oldLines,
        string[] newLines,
        DetectionMode mode = DetectionMode.Balanced)
    {
        var matches = new BlockMatch[candidates.Length];
        var threshold = DetectionModeThresholds.MovedConfidenceThreshold(mode);

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            var score = BlockSimilarityScorer.CombinedScore(candidate, oldLines, newLines);
            var matchType = score >= threshold ? ChangeType.Moved : ChangeType.Uncertain;
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
