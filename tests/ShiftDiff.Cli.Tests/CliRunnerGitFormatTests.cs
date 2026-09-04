using ShiftDiff.Cli;
using Xunit;

namespace ShiftDiff.Cli.Tests;

// FR-024 Patch Export: a git-compatible patch is more than a unified diff — it
// carries the `diff --git` header and the new/deleted file modes.
public class CliRunnerGitFormatTests : IDisposable {
  private readonly TempWorkspace _workspace = new();
  private readonly StringWriter _output = new();
  private readonly StringWriter _error = new();

  public void Dispose() => _workspace.Dispose();

  private string Out => _output.ToString();

  private int Run(params string[] args) => CliRunner.Run(args, _output, _error);

  [Fact]
  public void ExportPatch_GitFormat_EmitsTheGitDiffHeader() {
    var oldPath = _workspace.FileNamed("src/App.cs", "one\ntwo\n");
    var newPath = _workspace.FileNamed("out/App.cs", "one\nTWO\n");

    var exitCode = Run("export-patch", oldPath, newPath, "--format", "git");

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("diff --git", Out);
    Assert.Contains("@@", Out);
  }

  [Fact]
  public void ExportPatch_GitFormat_IsNotJustTheUnifiedDiff() {
    var oldPath = _workspace.File("one\n");
    var newPath = _workspace.File("two\n");

    Run("export-patch", oldPath, newPath, "--format", "git");
    var gitText = Out;

    var unifiedOutput = new StringWriter();
    CliRunner.Run(["export-patch", oldPath, newPath, "--format", "unified"], unifiedOutput, new StringWriter());

    Assert.NotEqual(unifiedOutput.ToString(), gitText);
  }

  [Fact]
  public void ExportPatch_GitFormat_NewFile_RecordsTheCreation() {
    var oldPath = _workspace.File(string.Empty);
    var newPath = _workspace.File("fresh\ncontent\n");

    Run("export-patch", oldPath, newPath, "--format", "git");

    Assert.Contains("new file mode", Out);
  }

  [Fact]
  public void ExportPatch_GitFormat_DeletedFile_RecordsTheDeletion() {
    var oldPath = _workspace.File("gone\ncontent\n");
    var newPath = _workspace.File(string.Empty);

    Run("export-patch", oldPath, newPath, "--format", "git");

    Assert.Contains("deleted file mode", Out);
  }

  [Fact]
  public void ExportPatch_GitFormat_RoundTripsThroughTheParser() {
    var oldPath = _workspace.File("one\ntwo\nthree\n");
    var newPath = _workspace.File("one\nTWO\nthree\n");
    var patchPath = _workspace.MissingPath(".patch");

    Run("export-patch", oldPath, newPath, "--format", "git", "--out", patchPath);

    var applyOutput = new StringWriter();
    var exitCode = CliRunner.Run(["apply-patch", oldPath, patchPath], applyOutput, new StringWriter());

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("TWO", applyOutput.ToString());
  }

  [Fact]
  public void Compare_GitFormat_AlsoEmitsTheGitHeader() {
    var oldPath = _workspace.File("one\n");
    var newPath = _workspace.File("two\n");

    Run("compare", oldPath, newPath, "--format", "git");

    Assert.Contains("diff --git", Out);
  }
}
