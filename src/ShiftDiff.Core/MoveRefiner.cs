namespace ShiftDiff.Core;

// R-001 False Move Detection, presentation side. BlockClassifier reports every
// matched block, including the ones that merely slid down because lines were
// inserted above them — showing those as "moved" buries the handful of blocks
// that genuinely changed place.
//
// Refine() answers "which blocks actually changed order?" in two steps:
//   1. Coalesce runs of adjacent blocks that share one displacement, so a
//      method that matched as six fragments is reported once.
//   2. Keep only the blocks that break the reading order: the heaviest
//      order-preserving subsequence (weighted longest increasing subsequence by
//      new-file position) is the stable spine of the file; everything off it is
//      a genuine relocation.
public static class MoveRefiner
{
    // Above this many blocks the O(n²) subsequence scan is not worth its cost;
    // such files are boilerplate-heavy and the raw list is already meaningless.
    private const int MaxBlocksForOrderAnalysis = 2000;

    // Lines that are never anchors (braces, blanks, boilerplate) split one
    // relocated region into several matched fragments. Fragments that share a
    // displacement and sit within this many lines of each other moved together.
    private const int DefaultMaxGap = 8;

    public static BlockMatch[] Refine(IReadOnlyList<BlockMatch> blocks) =>
        KeepReorderings(Coalesce(blocks));

    /// <summary>Merges neighbouring blocks that share one old→new displacement.</summary>
    public static BlockMatch[] Coalesce(IReadOnlyList<BlockMatch> blocks, int maxGap = DefaultMaxGap)
    {
        if (blocks.Count <= 1) return [.. blocks];

        var ordered = blocks.OrderBy(block => block.OldStart).ThenBy(block => block.NewStart).ToList();
        var merged = new List<BlockMatch>();
        var current = ordered[0];

        foreach (var next in ordered.Skip(1))
        {
            var sameDisplacement = next.NewStart - next.OldStart == current.NewStart - current.OldStart;
            var adjacent = next.OldStart <= current.OldEnd + 1 + maxGap && next.OldStart >= current.OldStart;
            var compatible = next.MatchType == current.MatchType;

            if (sameDisplacement && adjacent && compatible)
            {
                var lines = current.OldEnd - current.OldStart + 1 + (next.OldEnd - next.OldStart + 1);
                current = current with
                {
                    OldEnd = Math.Max(current.OldEnd, next.OldEnd),
                    NewEnd = Math.Max(current.NewEnd, next.NewEnd),
                    Score = lines == 0 ? current.Score : WeightedScore(current, next),
                    // Confidence is ordered strongest-first (Certain == 0).
                    Confidence = (Confidence)Math.Min((int)current.Confidence, (int)next.Confidence),
                };
                continue;
            }

            merged.Add(current);
            current = next;
        }

        merged.Add(current);
        return [.. merged];
    }

    /// <summary>Drops blocks whose position is explained by ordinary insertions and deletions.</summary>
    public static BlockMatch[] KeepReorderings(IReadOnlyList<BlockMatch> blocks)
    {
        if (blocks.Count <= 1) return [];
        if (blocks.Count > MaxBlocksForOrderAnalysis) return [.. blocks];

        var ordered = blocks.OrderBy(block => block.OldStart).ToArray();
        var weights = ordered.Select(Length).ToArray();
        var best = new long[ordered.Length];
        var previous = new int[ordered.Length];

        var bestEnd = 0;
        for (var i = 0; i < ordered.Length; i++)
        {
            best[i] = weights[i];
            previous[i] = -1;
            for (var j = 0; j < i; j++)
            {
                if (ordered[j].NewStart >= ordered[i].NewStart) continue;
                if (best[j] + weights[i] <= best[i]) continue;

                best[i] = best[j] + weights[i];
                previous[i] = j;
            }

            if (best[i] > best[bestEnd]) bestEnd = i;
        }

        var stable = new bool[ordered.Length];
        for (var index = bestEnd; index >= 0; index = previous[index])
        {
            stable[index] = true;
            if (previous[index] < 0) break;
        }

        return [.. ordered.Where((_, index) => !stable[index])];
    }

    private static long Length(BlockMatch block) => Math.Max(1, block.OldEnd - block.OldStart + 1);

    private static double WeightedScore(BlockMatch left, BlockMatch right)
    {
        var leftLength = Length(left);
        var rightLength = Length(right);
        return (left.Score * leftLength + right.Score * rightLength) / (leftLength + rightLength);
    }
}
