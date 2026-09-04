using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

// §7.3: two to four folder trees in one session, with the moves between them named.
public class WorkspaceComparisonSourceTests : IDisposable {
  private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-workspace", Guid.NewGuid().ToString("N"));

  public WorkspaceComparisonSourceTests() => Directory.CreateDirectory(_root);

  public void Dispose() {
    if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
  }

  [Fact]
  public void Constructor_WithFewerThanTwoFolders_Throws() {
    Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceComparisonSource([Folder("only")]));
  }

  [Fact]
  public void Constructor_WithMoreThanFourFolders_Throws() {
    var folders = Enumerable.Range(0, 5).Select(i => Folder($"f{i}")).ToArray();

    Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceComparisonSource(folders));
  }

  [Fact]
  public void Entries_ListChangedFilesAndSkipIdenticalOnes() {
    var left = Folder("left");
    var right = Folder("right");
    Write(left, "same.txt", "x\n");
    Write(right, "same.txt", "x\n");
    Write(left, "edited.txt", "one\n");
    Write(right, "edited.txt", "two\n");

    var source = new WorkspaceComparisonSource([left, right]);

    var entry = Assert.Single(source.Entries);
    Assert.Equal("edited.txt", entry.DisplayPath);
    Assert.Equal(ChangeType.Edited, entry.ChangeType);
  }

  [Fact]
  public void Entries_FileMovedBetweenFolders_IsOneRowMarkedAsMoved() {
    var left = Folder("left");
    var right = Folder("right");
    Write(left, "src/Parser.cs", "public class Parser { }\n");
    Write(right, "core/Parser.cs", "public class Parser { }\n");

    var source = new WorkspaceComparisonSource([left, right]);

    var entry = Assert.Single(source.Entries);
    Assert.Equal(ChangeType.Moved, entry.ChangeType);
    Assert.Contains("core/Parser.cs", entry.Detail);
  }

  [Fact]
  public void Relationships_AreReportedForMovedFiles() {
    var left = Folder("left");
    var right = Folder("right");
    Write(left, "src/Parser.cs", "public class Parser { }\n");
    Write(right, "core/Parser.cs", "public class Parser { }\n");

    var source = new WorkspaceComparisonSource([left, right]);

    Assert.Contains(source.Relationships, link => link.Kind == WorkspaceRelationshipKind.FileMoved);
  }

  [Fact]
  public void Load_MovedFile_ReadsBothEndsFromTheirOwnFolders() {
    var left = Folder("left");
    var right = Folder("right");
    Write(left, "src/Parser.cs", "public class Parser { int A; }\n");
    Write(right, "core/Parser.cs", "public class Parser { int B; }\n");

    var source = new WorkspaceComparisonSource([left, right]);
    var input = source.Load(source.Entries[0]);

    Assert.Contains("int A", System.Text.Encoding.UTF8.GetString(input.OldContent));
    Assert.Contains("int B", System.Text.Encoding.UTF8.GetString(input.NewContent));
  }

  [Fact]
  public void Load_FileMissingOnOneSide_YieldsAnEmptySide() {
    var left = Folder("left");
    var right = Folder("right");
    Write(left, "gone.txt", "content\n");

    var source = new WorkspaceComparisonSource([left, right]);
    var input = source.Load(source.Entries[0]);

    Assert.NotEmpty(input.OldContent);
    Assert.Empty(input.NewContent);
  }

  [Fact]
  public void Title_NamesEverySourceFolder() {
    var source = new WorkspaceComparisonSource([Folder("base"), Folder("local"), Folder("remote")]);

    Assert.Contains("base", source.Title);
    Assert.Contains("local", source.Title);
    Assert.Contains("remote", source.Title);
  }

  [Fact]
  public async Task Shell_OpeningThreeFolders_UsesTheWorkspaceSource() {
    var a = Folder("a");
    var b = Folder("b");
    var c = Folder("c");
    Write(a, "file.txt", "one\n");
    Write(b, "file.txt", "two\n");
    Write(c, "file.txt", "three\n");

    var shell = new ShellViewModel();
    await shell.OpenDroppedAsync([a, b, c]);

    Assert.Single(shell.Files);
    Assert.True(shell.ShowFileList);
  }

  private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;

  private static void Write(string root, string relativePath, string content) {
    var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content);
  }
}
