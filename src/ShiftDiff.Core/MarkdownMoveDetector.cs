namespace ShiftDiff.Core;

public static class MarkdownMoveDetector
{
    public static MarkdownChange[] Detect(IReadOnlyList<MarkdownChange> changes)
    {
        var removed = changes.Where(c => c.ChangeType == MarkdownChangeType.Removed).ToList();
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

        if (movedFromByPath.Count == 0)
        {
            return changes.ToArray();
        }

        return changes
            .Where(c => !(c.ChangeType == MarkdownChangeType.Removed && matchedRemoved.Contains(c.Path)))
            .Select(c => movedFromByPath.TryGetValue(c.Path, out var movedFrom)
                ? c with { ChangeType = MarkdownChangeType.Moved, MovedFrom = movedFrom }
                : c)
            .ToArray();
    }
}
