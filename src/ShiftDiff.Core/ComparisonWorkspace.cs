namespace ShiftDiff.Core;

public sealed record WorkspaceSource(
    string Id,
    string Label,
    IReadOnlyDictionary<string, byte[]> Files);

public sealed record WorkspaceCell(
    int SourceIndex,
    string RelativePath,
    FolderChangeType ChangeType,
    long Size);

public sealed record WorkspaceRow(
    string LogicalPath,
    IReadOnlyList<WorkspaceCell?> Cells);

public enum WorkspaceRelationshipKind {
  FileMoved,
  FileMovedEdited,
  FolderMoved,
}

public sealed record WorkspaceRelationship(
    WorkspaceRelationshipKind Kind,
    int SourceIndex,
    int TargetIndex,
    string SourcePath,
    string TargetPath,
    int SourceRow,
    int TargetRow);

public sealed record WorkspaceComparison(
    IReadOnlyList<WorkspaceSource> Sources,
    IReadOnlyList<WorkspaceRow> Rows,
    IReadOnlyList<WorkspaceRelationship> Relationships);

public static class ComparisonWorkspace {
  public static WorkspaceComparison Compare(params WorkspaceSource[] sources) {
    ArgumentNullException.ThrowIfNull(sources);
    if (sources.Length is < 2 or > 4) {
      throw new ArgumentOutOfRangeException(nameof(sources), "A workspace compares two to four sources.");
    }

    var baseSource = sources[0];
    var builders = baseSource.Files.Keys.ToDictionary(
        path => path,
        path => {
          var builder = new RowBuilder(path, sources.Length);
          builder.Cells[0] = Cell(0, path, FolderChangeType.Unchanged, baseSource.Files[path]);
          return builder;
        },
        StringComparer.Ordinal);
    var relationships = new List<RelationshipBuilder>();

    for (var targetIndex = 1; targetIndex < sources.Length; targetIndex++) {
      var target = sources[targetIndex];
      var changes = FolderComparer.Compare(baseSource.Files, target.Files);
      changes = FolderMoveDetector.Detect(changes, baseSource.Files, target.Files);
      changes = FolderRenameDetector.Detect(changes, baseSource.Files, target.Files);

      foreach (var change in changes) {
        if (change.ChangeType is FolderChangeType.Moved or FolderChangeType.MovedEdited) {
          var sourcePath = change.MovedFrom!;
          var row = GetOrAdd(builders, sourcePath, sources.Length);
          row.Cells[targetIndex] = Cell(targetIndex, change.RelativePath, change.ChangeType, target.Files[change.RelativePath]);
          relationships.Add(new RelationshipBuilder(
              change.ChangeType == FolderChangeType.Moved
                  ? WorkspaceRelationshipKind.FileMoved
                  : WorkspaceRelationshipKind.FileMovedEdited,
              0,
              targetIndex,
              sourcePath,
              change.RelativePath));
          continue;
        }

        var logicalPath = change.RelativePath;
        var builder = GetOrAdd(builders, logicalPath, sources.Length);
        if (target.Files.TryGetValue(change.RelativePath, out var content)) {
          builder.Cells[targetIndex] = Cell(targetIndex, change.RelativePath, change.ChangeType, content);
        }
      }

      relationships.AddRange(InferFolderMoves(relationships, baseSource, targetIndex));
    }

    var rows = builders.Values
        .OrderBy(builder => builder.LogicalPath, StringComparer.Ordinal)
        .Select(builder => new WorkspaceRow(builder.LogicalPath, builder.Cells))
        .ToArray();
    var rowIndexByCell = rows
        .SelectMany((row, rowIndex) => row.Cells.Where(cell => cell is not null).Select(cell => (cell: cell!, rowIndex)))
        .ToDictionary(pair => (pair.cell.SourceIndex, pair.cell.RelativePath), pair => pair.rowIndex);

    var materializedRelationships = relationships
        .DistinctBy(link => (link.Kind, link.SourceIndex, link.TargetIndex, link.SourcePath, link.TargetPath))
        .Select(link => new WorkspaceRelationship(
            link.Kind,
            link.SourceIndex,
            link.TargetIndex,
            link.SourcePath,
            link.TargetPath,
            RowFor(rowIndexByCell, link.SourceIndex, link.SourcePath),
            RowFor(rowIndexByCell, link.TargetIndex, link.TargetPath)))
        .ToArray();

    return new WorkspaceComparison(sources, rows, materializedRelationships);
  }

  private static IEnumerable<RelationshipBuilder> InferFolderMoves(
      IReadOnlyList<RelationshipBuilder> relationships,
      WorkspaceSource baseSource,
      int targetIndex) {
    var fileMoves = relationships
        .Where(link => link.TargetIndex == targetIndex && link.Kind is WorkspaceRelationshipKind.FileMoved or WorkspaceRelationshipKind.FileMovedEdited)
        .Select(link => new {
          Link = link,
          OldFolder = NormalizeFolder(Path.GetDirectoryName(link.SourcePath)),
          NewFolder = NormalizeFolder(Path.GetDirectoryName(link.TargetPath)),
        })
        .Where(entry => entry.OldFolder.Length > 0 && entry.OldFolder != entry.NewFolder)
        .GroupBy(entry => (entry.OldFolder, entry.NewFolder));

    foreach (var group in fileMoves) {
      var filesInOldFolder = baseSource.Files.Keys.Count(path => IsInFolder(path, group.Key.OldFolder));
      if (group.Count() >= 2 || group.Count() == filesInOldFolder) {
        yield return new RelationshipBuilder(
            WorkspaceRelationshipKind.FolderMoved,
            0,
            targetIndex,
            group.Key.OldFolder,
            group.Key.NewFolder);
      }
    }
  }

  private static bool IsInFolder(string path, string folder) =>
      NormalizeFolder(Path.GetDirectoryName(path)).Equals(folder, StringComparison.Ordinal);

  private static string NormalizeFolder(string? folder) => (folder ?? string.Empty).Replace('\\', '/');

  private static int RowFor(
      IReadOnlyDictionary<(int SourceIndex, string RelativePath), int> rowIndexByCell,
      int sourceIndex,
      string path) {
    if (rowIndexByCell.TryGetValue((sourceIndex, path), out var exact)) return exact;

    var child = rowIndexByCell
        .Where(pair => pair.Key.SourceIndex == sourceIndex && IsBelow(pair.Key.RelativePath, path))
        .Select(pair => pair.Value)
        .DefaultIfEmpty(0)
        .Min();
    return child;
  }

  private static bool IsBelow(string path, string folder) {
    var normalizedPath = path.Replace('\\', '/');
    var normalizedFolder = folder.Replace('\\', '/').TrimEnd('/');
    return normalizedPath.StartsWith(normalizedFolder + '/', StringComparison.Ordinal);
  }

  private static RowBuilder GetOrAdd(Dictionary<string, RowBuilder> builders, string path, int sourceCount) {
    if (builders.TryGetValue(path, out var existing)) return existing;
    var created = new RowBuilder(path, sourceCount);
    builders.Add(path, created);
    return created;
  }

  private static WorkspaceCell Cell(int sourceIndex, string path, FolderChangeType changeType, byte[] content) =>
      new(sourceIndex, path, changeType, content.LongLength);

  private sealed class RowBuilder(string logicalPath, int sourceCount) {
    public string LogicalPath { get; } = logicalPath;
    public WorkspaceCell?[] Cells { get; set; } = new WorkspaceCell?[sourceCount];
  }

  private sealed record RelationshipBuilder(
      WorkspaceRelationshipKind Kind,
      int SourceIndex,
      int TargetIndex,
      string SourcePath,
      string TargetPath);
}
