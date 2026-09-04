using ShiftDiff.Cli;
using Xunit;

namespace ShiftDiff.Cli.Tests;

public class CliRunnerTests : IDisposable {
  private readonly TempWorkspace _workspace = new();
  private readonly StringWriter _output = new();
  private readonly StringWriter _error = new();

  public void Dispose() => _workspace.Dispose();

  private string Out => _output.ToString();

  private string Err => _error.ToString();

  private int Run(params string[] args) => CliRunner.Run(args, _output, _error);

  [Fact]
  public void Run_TwoIdenticalFiles_ExitsZeroAndReportsNoChanges() {
    var oldPath = _workspace.File("one\ntwo\nthree\n");
    var newPath = _workspace.File("one\ntwo\nthree\n");

    var exitCode = Run(oldPath, newPath);

    Assert.Equal(ExitCode.NoDifferences, exitCode);
    Assert.Contains($"--- {oldPath}", Out);
    Assert.Contains($"+++ {newPath}", Out);
    Assert.Contains("0 added · 0 removed · 0 edited", Out);
    Assert.DoesNotContain("@@", Out);
    Assert.Equal(string.Empty, Err);
  }

  [Fact]
  public void Run_TwoDifferentFiles_ExitsWithDifferencesFound() {
    var oldPath = _workspace.File("one\ntwo\nthree\n");
    var newPath = _workspace.File("one\nTWO\nthree\n");

    var exitCode = Run(oldPath, newPath);

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("@@", Out);
  }

  [Fact]
  public void Run_EditedLine_SemanticOutputShowsInlineTokenChange() {
    var oldPath = _workspace.File("alpha\nvalue = 1\nomega\n");
    var newPath = _workspace.File("alpha\nvalue = 2\nomega\n");

    Run(oldPath, newPath);

    Assert.Contains("[-1-]{+2+}", Out);
    Assert.Contains("value", Out);
  }

  [Fact]
  public void Run_MovedBlock_SemanticOutputNamesTheMoveWithConfidence() {
    var oldPath = _workspace.File(MovedBlockOld, ".cs");
    var newPath = _workspace.File(MovedBlockNew, ".cs");

    var exitCode = Run("compare", oldPath, newPath, "--mode", "aggressive");

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Matches(@"old \d+-\d+ -> new \d+-\d+", Out);
  }

  [Fact]
  public void Run_WithEmojiFlag_UsesEmojiMarkers() {
    var oldPath = _workspace.File("one\ntwo\n");
    var newPath = _workspace.File("one\nTWO\n");

    Run("compare", oldPath, newPath, "--emoji");

    Assert.Contains("✏️", Out);
  }

  [Fact]
  public void Run_WithoutEmojiFlag_UsesPlainTextMarkers() {
    var oldPath = _workspace.File("one\ntwo\n");
    var newPath = _workspace.File("one\nTWO\n");

    Run("compare", oldPath, newPath);

    Assert.DoesNotContain("✏️", Out);
  }

  [Fact]
  public void Run_UnifiedFormat_EmitsPlainUnifiedDiff() {
    var oldPath = _workspace.File("one\ntwo\nthree\n");
    var newPath = _workspace.File("one\nTWO\nthree\n");

    var exitCode = Run("compare", oldPath, newPath, "--format", "unified");

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("-two", Out);
    Assert.Contains("+TWO", Out);
  }

  [Fact]
  public void Run_JsonFormat_EmitsMachineReadableSummaryAndChanges() {
    var oldPath = _workspace.File("one\ntwo\nthree\n", ".cs");
    var newPath = _workspace.File("one\nTWO\nthree\n", ".cs");

    var exitCode = Run("compare", oldPath, newPath, "--json");

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    using var document = System.Text.Json.JsonDocument.Parse(Out);
    var root = document.RootElement;
    Assert.Equal("C#", root.GetProperty("language").GetString());
    Assert.Equal(1, root.GetProperty("summary").GetProperty("edited").GetInt32());
    Assert.NotEmpty(root.GetProperty("changes").EnumerateArray());
  }

  [Fact]
  public void Run_JsonFormat_OmitsUnchangedLinesFromTheChangeArray() {
    var oldPath = _workspace.File("one\ntwo\nthree\n");
    var newPath = _workspace.File("one\nTWO\nthree\n");

    Run("compare", oldPath, newPath, "--json");

    using var document = System.Text.Json.JsonDocument.Parse(Out);
    var changes = document.RootElement.GetProperty("changes").EnumerateArray().ToArray();
    Assert.Single(changes);
    Assert.Equal("Edited", changes[0].GetProperty("type").GetString());
  }

  [Fact]
  public void Run_IgnoreCase_TreatsCaseOnlyEditsAsUnchanged() {
    var oldPath = _workspace.File("one\ntwo\n");
    var newPath = _workspace.File("one\nTWO\n");

    var exitCode = Run("compare", oldPath, newPath, "--ignore-case");

    Assert.Equal(ExitCode.NoDifferences, exitCode);
  }

  [Fact]
  public void Run_TwoIniFiles_PrintsFormattedIniChanges() {
    var oldPath = _workspace.File("[a]\nkey=1\nother=2\n", ".ini");
    var newPath = _workspace.File("[a]\nkey=1\nother=3\n", ".ini");

    var exitCode = Run(oldPath, newPath);

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("a.other: Changed 2 -> 3", Out);
    Assert.DoesNotContain("@@", Out);
    Assert.Equal(string.Empty, Err);
  }

  [Fact]
  public void Run_TwoIdenticalIniFiles_ExitsZero() {
    var oldPath = _workspace.File("[a]\nkey=1\n", ".ini");
    var newPath = _workspace.File("[a]\nkey=1\n", ".ini");

    Assert.Equal(ExitCode.NoDifferences, Run(oldPath, newPath));
  }

  [Fact]
  public void Run_TwoJsonFiles_PrintsFormattedJsonChanges() {
    var oldPath = _workspace.File("{\"a\":{\"key\":1,\"other\":2}}", ".json");
    var newPath = _workspace.File("{\"a\":{\"key\":1,\"other\":3}}", ".json");

    var exitCode = Run(oldPath, newPath);

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("a.other: Changed 2 -> 3", Out);
    Assert.DoesNotContain("a.key", Out);
    Assert.Equal(string.Empty, Err);
  }

  [Fact]
  public void Run_TwoXmlFiles_PrintsFormattedXmlChanges() {
    var oldPath = _workspace.File("<a><key>1</key><other>2</other></a>", ".xml");
    var newPath = _workspace.File("<a><key>1</key><other>3</other></a>", ".xml");

    var exitCode = Run(oldPath, newPath);

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("other: Changed 2 -> 3", Out);
    Assert.Equal(string.Empty, Err);
  }

  [Fact]
  public void Run_TwoMarkdownFiles_PrintsFormattedMarkdownChanges() {
    var oldPath = _workspace.File("# Old\ncontent\n", ".md");
    var newPath = _workspace.File("# New\ncontent\n", ".md");

    var exitCode = Run(oldPath, newPath);

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("# New: Moved (from # Old)", Out);
    Assert.Equal(string.Empty, Err);
  }

  [Fact]
  public void Run_StructuredFileWithUnifiedFormat_FallsBackToTheLineDiff() {
    var oldPath = _workspace.File("[a]\nkey=1\n", ".ini");
    var newPath = _workspace.File("[a]\nkey=2\n", ".ini");

    Run("compare", oldPath, newPath, "--format", "unified");

    Assert.Contains("@@", Out);
    Assert.Contains("-key=1", Out);
  }

  [Fact]
  public void Run_BinaryFiles_ReportsDifferenceWithoutAttemptingALineDiff() {
    var oldPath = Path.Combine(_workspace.Root, "a.bin");
    var newPath = Path.Combine(_workspace.Root, "b.bin");
    File.WriteAllBytes(oldPath, [0x00, 0x01, 0x02, 0x00]);
    File.WriteAllBytes(newPath, [0x00, 0x01, 0x03, 0x00]);

    var exitCode = Run(oldPath, newPath);

    Assert.Equal(ExitCode.DifferencesFound, exitCode);
    Assert.Contains("Binary files", Out);
    Assert.DoesNotContain("@@", Out);
  }

  [Fact]
  public void Run_IdenticalBinaryFiles_ExitsZero() {
    var oldPath = Path.Combine(_workspace.Root, "a.bin");
    var newPath = Path.Combine(_workspace.Root, "b.bin");
    File.WriteAllBytes(oldPath, [0x00, 0x01, 0x02, 0x00]);
    File.WriteAllBytes(newPath, [0x00, 0x01, 0x02, 0x00]);

    Assert.Equal(ExitCode.NoDifferences, Run(oldPath, newPath));
    Assert.Contains("identical", Out);
  }

  [Fact]
  public void Run_NoArgs_PrintsUsageAndExitsZero() {
    var exitCode = Run();

    Assert.Equal(ExitCode.NoDifferences, exitCode);
    Assert.Contains("usage", Out, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void Run_OneArg_ExitsWithInvalidInputAndWritesUsageToError() {
    var exitCode = Run("only-one-arg");

    Assert.Equal(ExitCode.InvalidInput, exitCode);
    Assert.Contains("usage", Err, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(string.Empty, Out);
  }

  [Fact]
  public void Run_UnknownOption_ExitsWithInvalidInput() {
    var exitCode = Run("compare", "a", "b", "--nonsense");

    Assert.Equal(ExitCode.InvalidInput, exitCode);
    Assert.Contains("--nonsense", Err);
  }

  [Fact]
  public void Run_Version_PrintsVersionAndExitsZero() {
    Assert.Equal(ExitCode.NoDifferences, Run("--version"));
    Assert.Contains("shiftdiff", Out);
  }

  [Fact]
  public void Run_MissingFile_ExitsWithInvalidInputAndFriendlyError() {
    var newPath = _workspace.File("one\n");
    var missingPath = _workspace.MissingPath();

    var exitCode = Run(missingPath, newPath);

    Assert.Equal(ExitCode.InvalidInput, exitCode);
    Assert.Contains(missingPath, Err);
    Assert.DoesNotContain("StackTrace", Err);
    Assert.Equal(string.Empty, Out);
  }

  [Fact]
  public void Run_UnreadableFile_ExitsWithInvalidInputNotAnUnhandledException() {
    if (OperatingSystem.IsWindows()) return;

    var newPath = _workspace.File("one\n");
    var unreadablePath = _workspace.File("two\n");
    File.SetUnixFileMode(unreadablePath, UnixFileMode.None);

    try {
      var exitCode = Run(unreadablePath, newPath);

      Assert.Equal(ExitCode.InvalidInput, exitCode);
      Assert.Contains(unreadablePath, Err);
      Assert.DoesNotContain("StackTrace", Err);
    } finally {
      File.SetUnixFileMode(unreadablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
  }

  [Fact]
  public void Run_MixedFileAndFolderOperands_ExitsWithInvalidInput() {
    var file = _workspace.File("one\n");
    var folder = _workspace.Folder("dir");

    var exitCode = Run("compare", file, folder);

    Assert.Equal(ExitCode.InvalidInput, exitCode);
    Assert.Contains("two files or two folders", Err);
  }

  private const string MovedBlockOld = """
        public class Sample
        {
            public bool Validate(int value)
            {
                if (value < 0)
                {
                    return false;
                }

                return true;
            }

            public string Describe()
            {
                return "sample";
            }
        }
        """;

  private const string MovedBlockNew = """
        public class Sample
        {
            public string Describe()
            {
                return "sample";
            }

            public bool Validate(int value)
            {
                if (value < 0)
                {
                    return false;
                }

                return true;
            }
        }
        """;
}
