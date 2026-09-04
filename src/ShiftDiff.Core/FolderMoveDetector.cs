namespace ShiftDiff.Core;

public static class FolderMoveDetector {
  public static FolderEntryChange[] Detect(
      IReadOnlyList<FolderEntryChange> changes,
      IReadOnlyDictionary<string, byte[]> baseFiles,
      IReadOnlyDictionary<string, byte[]> targetFiles) {
    var removed = changes.Where(c => c.ChangeType == FolderChangeType.Removed).ToList();
    var matchedRemoved = new HashSet<string>();
    var movedFromByAddedPath = new Dictionary<string, string>();

    foreach (var added in changes.Where(c => c.ChangeType == FolderChangeType.Added)) {
      var addedContent = targetFiles[added.RelativePath];
      var candidates = removed
          .Where(r => !matchedRemoved.Contains(r.RelativePath))
          .Where(r => BinaryFileDetector.AreEqual(baseFiles[r.RelativePath], addedContent))
          .ToList();

      if (candidates.Count == 1) {
        matchedRemoved.Add(candidates[0].RelativePath);
        movedFromByAddedPath[added.RelativePath] = candidates[0].RelativePath;
      }
    }

    if (movedFromByAddedPath.Count == 0) {
      return changes.ToArray();
    }

    return changes
        .Where(c => !(c.ChangeType == FolderChangeType.Removed && matchedRemoved.Contains(c.RelativePath)))
        .Select(c => movedFromByAddedPath.TryGetValue(c.RelativePath, out var movedFrom)
            ? c with { ChangeType = FolderChangeType.Moved, MovedFrom = movedFrom }
            : c)
        .ToArray();
  }
}
