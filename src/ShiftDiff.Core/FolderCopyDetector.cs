namespace ShiftDiff.Core;

public static class FolderCopyDetector {
  public static FolderEntryChange[] Detect(
      IReadOnlyList<FolderEntryChange> changes,
      IReadOnlyDictionary<string, byte[]> targetFiles) {
    var survivors = changes
        .Where(c => c.ChangeType is FolderChangeType.Unchanged
            or FolderChangeType.Changed)
        .ToList();
    var copiedFromByAddedPath = new Dictionary<string, string>();

    foreach (var added in changes.Where(c => c.ChangeType == FolderChangeType.Added)) {
      var addedContent = targetFiles[added.RelativePath];
      var candidates = survivors
          .Where(s => s.RelativePath != added.RelativePath)
          .Where(s => BinaryFileDetector.AreEqual(targetFiles[s.RelativePath], addedContent))
          .ToList();

      if (candidates.Count == 1) {
        copiedFromByAddedPath[added.RelativePath] = candidates[0].RelativePath;
      }
    }

    if (copiedFromByAddedPath.Count == 0) {
      return changes.ToArray();
    }

    return changes
        .Select(c => copiedFromByAddedPath.TryGetValue(c.RelativePath, out var copiedFrom)
            ? c with { ChangeType = FolderChangeType.Copied, CopiedFrom = copiedFrom }
            : c)
        .ToArray();
  }
}
