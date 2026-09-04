namespace ShiftDiff.Core;

public enum FolderChangeType { Added, Removed, Changed, Unchanged, Moved, Copied, MovedEdited }

public sealed record FolderEntryChange(
    string RelativePath,
    FolderChangeType ChangeType,
    string? MovedFrom = null,
    long? Size = null,
    string? CopiedFrom = null);

public static class FolderComparer {
  public static FolderEntryChange[] Compare(
      IReadOnlyDictionary<string, byte[]> baseFiles,
      IReadOnlyDictionary<string, byte[]> targetFiles) {
    var paths = baseFiles.Keys.Union(targetFiles.Keys).OrderBy(p => p, StringComparer.Ordinal);
    var result = new List<FolderEntryChange>();
    foreach (var path in paths) {
      var inBase = baseFiles.TryGetValue(path, out var baseContent);
      var inTarget = targetFiles.TryGetValue(path, out var targetContent);
      var type = (inBase, inTarget) switch {
        (true, false) => FolderChangeType.Removed,
        (false, true) => FolderChangeType.Added,
        _ => BinaryFileDetector.AreEqual(baseContent!, targetContent!)
            ? FolderChangeType.Unchanged : FolderChangeType.Changed,
      };
      var size = inTarget ? targetContent!.Length : baseContent!.Length;
      result.Add(new FolderEntryChange(path, type, Size: size));
    }
    return result.ToArray();
  }
}
