namespace ShiftDiff.Core;

public static class FolderCopyDetector
{
    public static FolderEntryChange[] Detect(
        IReadOnlyList<FolderEntryChange> changes,
        IReadOnlyDictionary<string, byte[]> baseFiles,
        IReadOnlyDictionary<string, byte[]> targetFiles)
    {
        var stillPresentSourcePaths = baseFiles.Keys.Where(targetFiles.ContainsKey).ToList();
        var copiedFromByAddedPath = new Dictionary<string, string>();

        foreach (var added in changes.Where(c => c.ChangeType == FolderChangeType.Added))
        {
            var addedContent = targetFiles[added.RelativePath];
            var candidates = stillPresentSourcePaths
                .Where(path => path != added.RelativePath)
                .Where(path => BinaryFileDetector.AreEqual(baseFiles[path], addedContent))
                .ToList();

            if (candidates.Count == 1)
            {
                copiedFromByAddedPath[added.RelativePath] = candidates[0];
            }
        }

        if (copiedFromByAddedPath.Count == 0)
        {
            return changes.ToArray();
        }

        return changes
            .Select(c => copiedFromByAddedPath.TryGetValue(c.RelativePath, out var copiedFrom)
                ? c with { CopiedFrom = copiedFrom }
                : c)
            .ToArray();
    }
}
