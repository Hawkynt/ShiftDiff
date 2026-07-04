namespace ShiftDiff.Core;

public static class BlockClassifier
{
    public static BlockMatch[] Classify(BlockCandidate[] candidates)
    {
        var matches = new BlockMatch[candidates.Length];

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            matches[index] = new BlockMatch(
                candidate.OldStart,
                candidate.OldEnd,
                candidate.NewStart,
                candidate.NewEnd,
                ChangeType.Moved);
        }

        return matches;
    }
}
