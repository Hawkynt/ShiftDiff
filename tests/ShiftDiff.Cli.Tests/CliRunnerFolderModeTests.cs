using ShiftDiff.Cli;
using Xunit;

namespace ShiftDiff.Cli.Tests;

// FR-004 / MVP "basic folder comparison": the folder engine reached the CLI.
public class CliRunnerFolderModeTests : IDisposable {
  private readonly TempWorkspace _workspace = new();
  private readonly StringWriter _output = new();
  private readonly StringWriter _error = new();

  public void Dispose() => _workspace.Dispose();

  private string Out => _output.ToString();

  private int Run(params string[] args) => CliRunner.Run(args, _output, _error);

  [Fact]
  public void Compare_TwoIdenticalFolders_ExitsWithNoDifferences() {
    var left = _workspace.Folder("left");
    var right = _workspace.Folder("right");
    File.WriteAllText(Path.Combine(left, "a.txt"), "same\n");
    File.WriteAllText(Path.Combine(right, "a.txt"), "same\n");

    var exitCode = Run("compare", left, right);

    Assert.Equal(ExitCode.NoDifferences, exitCode);
    Assert.Contains("1 unchanged", Out);
  }

  [Fact]
  public void Compare_FolderWithAddedRemovedAndChangedFiles_ListsEachChange() {
    var left = _workspace.Folder("left");
    var right = _workspace.Folder("right");
    File.WriteAllText(Path.Combine(left, "gone.txt"), "x\n");
    File.WriteAllText(Path.Combine(left, "same.txt"), "x\n");
    File.WriteAllText(Path.Combine(left, "edit.txt"), "x\n");
    File.WriteAllText(Path.Combine(right, "same.txt"), "x\n");
    File.WriteAllText(Path.Combine(right, "edit.txt"), "y\n");
    File.WriteAllText(Path.Combine(right, "fresh.txt"), "z\n");

    var exitCode = Run("compare", left, right);

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("removed", Out);
    Assert.Contains("gone.txt", Out);
    Assert.Contains("added", Out);
    Assert.Contains("fresh.txt", Out);
    Assert.Contains("changed", Out);
    Assert.Contains("edit.txt", Out);
    Assert.DoesNotContain("same.txt", Out);
  }

  [Fact]
  public void Compare_FileMovedToAnotherSubfolder_IsReportedAsMovedWithItsOrigin() {
    var left = _workspace.Folder("left");
    var right = _workspace.Folder("right");
    Directory.CreateDirectory(Path.Combine(left, "old"));
    Directory.CreateDirectory(Path.Combine(right, "new"));
    File.WriteAllText(Path.Combine(left, "old", "thing.txt"), "content\n");
    File.WriteAllText(Path.Combine(right, "new", "thing.txt"), "content\n");

    var exitCode = Run("compare", left, right);

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("moved", Out);
    Assert.Contains("from old/thing.txt", Out);
  }

  [Fact]
  public void Compare_FoldersAsJson_EmitsAnEntryPerChange() {
    var left = _workspace.Folder("left");
    var right = _workspace.Folder("right");
    File.WriteAllText(Path.Combine(left, "a.txt"), "one\n");
    File.WriteAllText(Path.Combine(right, "a.txt"), "two\n");

    var exitCode = Run("compare", left, right, "--json");

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    using var document = System.Text.Json.JsonDocument.Parse(Out);
    var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray());
    Assert.Equal("a.txt", entry.GetProperty("path").GetString());
    Assert.Equal("Changed", entry.GetProperty("type").GetString());
  }

  [Fact]
  public void Compare_NestedFolders_UsesForwardSlashRelativePaths() {
    var left = _workspace.Folder("left");
    var right = _workspace.Folder("right");
    Directory.CreateDirectory(Path.Combine(left, "deep", "deeper"));
    File.WriteAllText(Path.Combine(left, "deep", "deeper", "file.txt"), "x\n");

    Run("compare", left, right);

    Assert.Contains("deep/deeper/file.txt", Out);
  }
}
