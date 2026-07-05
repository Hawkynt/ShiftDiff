namespace ShiftDiff.Core;

public static class BlockBuilder
{
    public static BlockCandidate[] Build(string[] oldLines, string[] newLines)
    {
        var oldAnchors = AnchorDetector.Detect(oldLines);
        var newAnchors = AnchorDetector.Detect(newLines);

        // Hash each line once here rather than recalling LineHasher.Hash per
        // anchor (Detect already hashed every line internally, but doesn't
        // expose the result) — avoids a second full-file SHA-256 pass per side.
        var oldHashes = oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();
        var newHashes = newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();

        var newStrongAnchorIndicesByHash = newAnchors
            .Where(anchor => anchor.Quality == AnchorQuality.Strong)
            .ToDictionary(
                anchor => newHashes[anchor.Index],
                anchor => anchor.Index);

        var rawMatchPairs = new List<(int OldIndex, int NewIndex)>();

        foreach (var oldAnchor in oldAnchors.Where(anchor => anchor.Quality == AnchorQuality.Strong))
        {
            var oldHash = oldHashes[oldAnchor.Index];

            if (!newStrongAnchorIndicesByHash.TryGetValue(oldHash, out var newIndex) || newIndex == oldAnchor.Index)
            {
                continue;
            }

            rawMatchPairs.Add((oldAnchor.Index, newIndex));
        }

        rawMatchPairs.Sort((left, right) => left.OldIndex.CompareTo(right.OldIndex));

        var candidates = new List<BlockCandidate>();

        for (var index = 0; index < rawMatchPairs.Count; index++)
        {
            var firstPair = rawMatchPairs[index];
            var lastPair = firstPair;

            while (index + 1 < rawMatchPairs.Count)
            {
                var nextPair = rawMatchPairs[index + 1];

                if (nextPair.OldIndex != lastPair.OldIndex + 1 || nextPair.NewIndex != lastPair.NewIndex + 1)
                {
                    break;
                }

                lastPair = nextPair;
                index++;
            }

            candidates.Add(new BlockCandidate(
                firstPair.OldIndex,
                lastPair.OldIndex,
                firstPair.NewIndex,
                lastPair.NewIndex));
        }

        return candidates.ToArray();
    }
}
