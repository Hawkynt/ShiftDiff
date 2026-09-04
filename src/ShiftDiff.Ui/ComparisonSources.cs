using System.Text;
using ShiftDiff.Core;
using ShiftDiff.Vcs;

namespace ShiftDiff.Ui;

/// <summary>One row of the file-list sidebar (FR-045).</summary>
public sealed record FileListEntry(string DisplayPath, ChangeType ChangeType, string Detail, object? Tag = null) {
  public string Name => DisplayPath.Contains('/') ? DisplayPath[(DisplayPath.LastIndexOf('/') + 1)..] : DisplayPath;

  public string Folder => DisplayPath.Contains('/') ? DisplayPath[..DisplayPath.LastIndexOf('/')] : string.Empty;

  public string Marker => ChangeMarker.Text(ChangeType);

  public string EmojiMarker => ChangeMarker.Emoji(ChangeType);
}

public sealed record ComparisonInput(byte[] OldContent, byte[] NewContent, string OldTitle, string NewTitle);

/// <summary>Where a comparison's files come from: a pair, a folder tree, or a repository.</summary>
public interface IComparisonSource {
  string Title { get; }

  IReadOnlyList<FileListEntry> Entries { get; }

  ComparisonInput Load(FileListEntry entry);
}

public sealed class FilePairSource(string oldPath, string newPath) : IComparisonSource {
  public string Title { get; } = $"{Path.GetFileName(oldPath)} ↔ {Path.GetFileName(newPath)}";

  public IReadOnlyList<FileListEntry> Entries { get; } =
      [new FileListEntry(Path.GetFileName(newPath), ChangeType.Edited, Path.GetDirectoryName(newPath) ?? string.Empty)];

  public ComparisonInput Load(FileListEntry entry) =>
      new(File.ReadAllBytes(oldPath), File.ReadAllBytes(newPath), oldPath, newPath);
}

// FR-004 folder comparison, including the move/copy/rename detectors.
public sealed class FolderComparisonSource : IComparisonSource {
  private readonly string _basePath;
  private readonly string _targetPath;

  public FolderComparisonSource(string basePath, string targetPath) {
    _basePath = basePath;
    _targetPath = targetPath;
    Title = $"{Path.GetFileName(basePath.TrimEnd(Path.DirectorySeparatorChar))} ↔ {Path.GetFileName(targetPath.TrimEnd(Path.DirectorySeparatorChar))}";

    var baseFiles = Read(basePath);
    var targetFiles = Read(targetPath);
    var changes = FolderComparer.Compare(baseFiles, targetFiles);
    changes = FolderMoveDetector.Detect(changes, baseFiles, targetFiles);
    changes = FolderCopyDetector.Detect(changes, targetFiles);
    changes = FolderRenameDetector.Detect(changes, baseFiles, targetFiles);

    Entries =
    [
        .. changes
                .Where(change => change.ChangeType != FolderChangeType.Unchanged)
                .Select(change => new FileListEntry(
                    change.RelativePath,
                    ToChangeType(change.ChangeType),
                    change.MovedFrom is { } from ? $"from {from}" : change.CopiedFrom is { } copy ? $"copy of {copy}" : Describe(change),
                    change)),
        ];
  }

  public string Title { get; }

  public IReadOnlyList<FileListEntry> Entries { get; }

  public ComparisonInput Load(FileListEntry entry) {
    var change = (FolderEntryChange)entry.Tag!;
    var oldRelative = change.MovedFrom ?? change.CopiedFrom ?? change.RelativePath;
    var oldFile = Path.Combine(_basePath, oldRelative.Replace('/', Path.DirectorySeparatorChar));
    var newFile = Path.Combine(_targetPath, change.RelativePath.Replace('/', Path.DirectorySeparatorChar));

    return new ComparisonInput(
        File.Exists(oldFile) ? File.ReadAllBytes(oldFile) : [],
        File.Exists(newFile) ? File.ReadAllBytes(newFile) : [],
        oldFile,
        newFile);
  }

  private static string Describe(FolderEntryChange change) =>
      change.Size is { } size ? $"{size:N0} bytes" : string.Empty;

  private static Dictionary<string, byte[]> Read(string root) {
    var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
      files[Path.GetRelativePath(root, path).Replace('\\', '/')] = File.ReadAllBytes(path);
    }

    return files;
  }

  private static ChangeType ToChangeType(FolderChangeType type) => type switch {
    FolderChangeType.Added => ChangeType.Added,
    FolderChangeType.Removed => ChangeType.Removed,
    FolderChangeType.Changed => ChangeType.Edited,
    FolderChangeType.Moved => ChangeType.Moved,
    FolderChangeType.MovedEdited => ChangeType.MovedEdited,
    FolderChangeType.Copied => ChangeType.Split,
    _ => ChangeType.Unchanged,
  };
}

// FR-042/§7.3: two to four folder trees aligned into one list, with the moves
// between them named. Backed by ComparisonWorkspace so the alignment and the
// relationship inference live in the engine, not in the UI.
public sealed class WorkspaceComparisonSource : IComparisonSource {
  private readonly WorkspaceComparison _comparison;
  private readonly IReadOnlyList<string> _roots;

  public WorkspaceComparisonSource(IReadOnlyList<string> folders) {
    ArgumentNullException.ThrowIfNull(folders);
    if (folders.Count is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(folders), "A workspace compares two to four folders.");

    _roots = folders;
    var sources = folders
        .Select((folder, index) => new WorkspaceSource(
            index.ToString(), Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)), Read(folder)))
        .ToArray();

    _comparison = ComparisonWorkspace.Compare(sources);
    Title = string.Join(" ↔ ", sources.Select(source => source.Label));

    Entries =
    [
        .. _comparison.Rows
                .Where(row => row.Cells.Any(cell => cell is not null && cell.ChangeType != FolderChangeType.Unchanged)
                              || row.Cells.Any(cell => cell is null))
                .Select(row => new FileListEntry(row.LogicalPath, ChangeTypeOf(row), Describe(row), row)),
        ];
  }

  public string Title { get; }

  public IReadOnlyList<FileListEntry> Entries { get; }

  public IReadOnlyList<WorkspaceRelationship> Relationships => _comparison.Relationships;

  public ComparisonInput Load(FileListEntry entry) {
    var row = (WorkspaceRow)entry.Tag!;
    var left = row.Cells[0];
    var right = row.Cells.Skip(1).LastOrDefault(cell => cell is not null) ?? row.Cells[^1];

    return new ComparisonInput(
        ReadCell(left, 0), ReadCell(right, IndexOf(row, right)),
        PathOf(left, 0), PathOf(right, IndexOf(row, right)));
  }

  private static int IndexOf(WorkspaceRow row, WorkspaceCell? cell) =>
      cell is null ? row.Cells.Count - 1 : cell.SourceIndex;

  private byte[] ReadCell(WorkspaceCell? cell, int sourceIndex) {
    if (cell is null) return [];

    var path = Path.Combine(_roots[sourceIndex], cell.RelativePath.Replace('/', Path.DirectorySeparatorChar));
    return File.Exists(path) ? File.ReadAllBytes(path) : [];
  }

  private string PathOf(WorkspaceCell? cell, int sourceIndex) =>
      cell is null ? "(absent)" : Path.Combine(_roots[sourceIndex], cell.RelativePath.Replace('/', Path.DirectorySeparatorChar));

  private static ChangeType ChangeTypeOf(WorkspaceRow row) {
    var target = row.Cells.Skip(1).LastOrDefault(cell => cell is not null);
    if (target is null) return ChangeType.Removed;
    if (row.Cells[0] is null) return ChangeType.Added;

    return target.ChangeType switch {
      FolderChangeType.Moved => ChangeType.Moved,
      FolderChangeType.MovedEdited => ChangeType.MovedEdited,
      FolderChangeType.Added => ChangeType.Added,
      FolderChangeType.Removed => ChangeType.Removed,
      FolderChangeType.Copied => ChangeType.Split,
      FolderChangeType.Changed => ChangeType.Edited,
      _ => ChangeType.Unchanged,
    };
  }

  private static string Describe(WorkspaceRow row) {
    var paths = row.Cells
        .Where(cell => cell is not null)
        .Select(cell => cell!.RelativePath)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    return paths.Length > 1 ? string.Join(" → ", paths) : $"{row.Cells.Count(cell => cell is null)} side(s) missing";
  }

  private static Dictionary<string, byte[]> Read(string root) {
    var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
      files[Path.GetRelativePath(root, path).Replace('\\', '/')] = File.ReadAllBytes(path);
    }

    return files;
  }
}

// AC-006/AC-007: a repository's changed files, opened straight into the viewer.
public sealed class VcsComparisonSource : IComparisonSource {
  private readonly VcsWorkspace _workspace;
  private readonly string _fromRevision;
  private readonly string _toRevision;

  public VcsComparisonSource(
      VcsWorkspace workspace,
      string fromRevision = VcsRevisions.Head,
      string toRevision = VcsRevisions.WorkingTree) {
    _workspace = workspace;
    _fromRevision = fromRevision;
    _toRevision = toRevision;
    Title = $"{workspace.Provider.Kind}: {Path.GetFileName(workspace.Root.TrimEnd(Path.DirectorySeparatorChar))}";

    Entries =
    [
        .. workspace.ListChanges(fromRevision, toRevision)
                .Select(status => new FileListEntry(
                    status.Path,
                    ToChangeType(status.Kind),
                    status.OriginalPath is { } original ? $"from {original}" : status.Kind.ToString().ToLowerInvariant(),
                    status)),
        ];
  }

  public string Title { get; }

  public IReadOnlyList<FileListEntry> Entries { get; }

  public ComparisonInput Load(FileListEntry entry) {
    var comparison = _workspace.Load((VcsFileStatus)entry.Tag!, _fromRevision, _toRevision);
    return new ComparisonInput(
        comparison.OldContent, comparison.NewContent, comparison.OldPath, comparison.NewPath);
  }

  private static ChangeType ToChangeType(VcsChangeKind kind) => kind switch {
    VcsChangeKind.Added or VcsChangeKind.Untracked => ChangeType.Added,
    VcsChangeKind.Deleted => ChangeType.Removed,
    VcsChangeKind.Modified => ChangeType.Edited,
    VcsChangeKind.Renamed => ChangeType.Moved,
    VcsChangeKind.Copied => ChangeType.Split,
    VcsChangeKind.Conflicted => ChangeType.Conflict,
    _ => ChangeType.Unchanged,
  };
}

// Lets tests (and previews) drive the shell without touching the filesystem.
public sealed class InMemoryComparisonSource(
    string title, IReadOnlyList<(FileListEntry Entry, ComparisonInput Input)> files) : IComparisonSource {
  public string Title { get; } = title;

  public IReadOnlyList<FileListEntry> Entries { get; } = [.. files.Select(file => file.Entry)];

  public ComparisonInput Load(FileListEntry entry) =>
      files.First(file => file.Entry.DisplayPath == entry.DisplayPath).Input;

  public static InMemoryComparisonSource FromText(string name, string oldText, string newText) =>
      new(name,
      [
          (new FileListEntry(name, ChangeType.Edited, string.Empty),
                new ComparisonInput(Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText), "old/" + name, "new/" + name)),
      ]);
}
