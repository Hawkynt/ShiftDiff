namespace ShiftDiff.Core;

public static class MarkdownMoveDetector
{
    // TokenShingleSimilarity alone, not FolderRenameDetector's 3-metric composite: measured directly
    // against short Markdown section bodies (tasks/0143 pre-scout, cycle 829) and found SimHash/
    // BlockSizeRatio dilute the signal on short, equal-length bodies (an unrelated-content fixture
    // scored 0.609 on the composite, higher than a genuinely-edited fixture at 0.592). TokenShingleSimilarity
    // alone scored 0.000 on every unrelated fixture tested and >=0.143 on every genuinely-edited one,
    // so a low threshold cleanly separates the two without the other metrics' noise.
    private const double FuzzyThreshold = 0.1;

    public static MarkdownChange[] Detect(IReadOnlyList<MarkdownChange> changes)
    {
        var removed = changes.Where(c => c.ChangeType == MarkdownChangeType.Removed).ToList();
        var removedByPath = removed.ToDictionary(r => r.Path);
        var matchedRemoved = new HashSet<string>();
        var movedFromByPath = new Dictionary<string, string>();

        foreach (var added in changes.Where(c => c.ChangeType == MarkdownChangeType.Added))
        {
            var candidates = removed
                .Where(r => !matchedRemoved.Contains(r.Path))
                .Where(r => r.OldValue == added.NewValue)
                .ToList();

            if (candidates.Count == 1)
            {
                matchedRemoved.Add(candidates[0].Path);
                movedFromByPath[added.Path] = candidates[0].Path;
            }
        }

        var movedEditedFromByPath = new Dictionary<string, string>();

        foreach (var added in changes.Where(c => c.ChangeType == MarkdownChangeType.Added && !movedFromByPath.ContainsKey(c.Path)))
        {
            var newLines = added.NewValue!.Split('\n');

            var candidates = removed
                .Where(r => !matchedRemoved.Contains(r.Path))
                .Where(r => Similarity(r.OldValue!.Split('\n'), newLines) >= FuzzyThreshold)
                .ToList();

            if (candidates.Count == 1)
            {
                matchedRemoved.Add(candidates[0].Path);
                movedEditedFromByPath[added.Path] = candidates[0].Path;
            }
        }

        if (movedFromByPath.Count == 0 && movedEditedFromByPath.Count == 0)
        {
            return changes.ToArray();
        }

        return changes
            .Where(c => !(c.ChangeType == MarkdownChangeType.Removed && matchedRemoved.Contains(c.Path)))
            .Select(c =>
            {
                if (movedFromByPath.TryGetValue(c.Path, out var movedFrom))
                {
                    return c with { ChangeType = MarkdownChangeType.Moved, MovedFrom = movedFrom };
                }

                if (movedEditedFromByPath.TryGetValue(c.Path, out var movedEditedFrom))
                {
                    var oldValue = removedByPath[movedEditedFrom].OldValue!;
                    return c with
                    {
                        ChangeType = MarkdownChangeType.MovedEdited,
                        MovedFrom = movedEditedFrom,
                        OldValue = oldValue,
                        BodyChanges = LineDiffer.Diff(oldValue.Split('\n'), c.NewValue!.Split('\n')),
                    };
                }

                return c;
            })
            .ToArray();
    }

    // Whole-section-body spans (like FolderRenameDetector's whole-file spans) can differ in line
    // count, so only the offset-independent metric is used — ExactHashOverlap/NormalizedHashOverlap
    // assume equal-length paired offsets and would index out of range.
    private static double Similarity(string[] oldLines, string[] newLines)
    {
        if (oldLines.Length == 0 || newLines.Length == 0)
        {
            return 0.0;
        }

        var candidate = new BlockCandidate(0, oldLines.Length - 1, 0, newLines.Length - 1);
        return BlockSimilarityScorer.TokenShingleSimilarity(candidate, oldLines, newLines);
    }
}
