namespace ShiftDiff.Core;

public static class BlockSimilarityScorer
{
    public static double ExactHashOverlap(BlockCandidate candidate, string[] oldLines, string[] newLines)
    {
        var lineCount = candidate.OldEnd - candidate.OldStart + 1;
        var matchingLines = 0;

        for (var offset = 0; offset < lineCount; offset++)
        {
            var oldHash = LineHasher.Hash(oldLines[candidate.OldStart + offset]).Raw;
            var newHash = LineHasher.Hash(newLines[candidate.NewStart + offset]).Raw;

            if (oldHash == newHash)
            {
                matchingLines++;
            }
        }

        return matchingLines / (double)lineCount;
    }
}
