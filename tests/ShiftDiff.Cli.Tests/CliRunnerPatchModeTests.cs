using System.Text;
using ShiftDiff.Cli;
using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Cli.Tests;

public class CliRunnerPatchModeTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public void Dispose() => _workspace.Dispose();

    private string Out => _output.ToString();

    private string Err => _error.ToString();

    private int Run(params string[] args) => CliRunner.Run(args, _output, _error);

    [Fact]
    public void ApplyPatch_CleanPatch_ReconstructsTargetAndReportsDifferences()
    {
        var oldText = "one\ntwo\nthree\n";
        var newText = "one\nTWO\nthree\n";
        var patchPath = _workspace.File(PatchBetween(oldText, newText), ".patch");
        var sourcePath = _workspace.File(oldText);

        var exitCode = Run("apply-patch", sourcePath, patchPath);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Equal(newText.Replace("\n", Environment.NewLine), Out);
        Assert.Equal(string.Empty, Err);
    }

    [Fact]
    public void ApplyPatch_LegacyPatchAndSourceFlags_StillWork()
    {
        var oldText = "one\ntwo\nthree\n";
        var newText = "one\nTWO\nthree\n";
        var patchPath = _workspace.File(PatchBetween(oldText, newText), ".patch");
        var sourcePath = _workspace.File(oldText);

        var exitCode = Run("--patch", patchPath, "--source", sourcePath);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Equal(newText.Replace("\n", Environment.NewLine), Out);
    }

    [Fact]
    public void ApplyPatch_NoOpPatch_ExitsWithNoDifferences()
    {
        var text = "one\ntwo\n";
        var patchPath = _workspace.File(PatchBetween(text, text), ".patch");
        var sourcePath = _workspace.File(text);

        Assert.Equal(ExitCode.NoDifferences, Run("apply-patch", sourcePath, patchPath));
    }

    [Fact]
    public void ApplyPatch_WithOutFile_WritesResultToDiskInsteadOfStdout()
    {
        var oldText = "one\ntwo\nthree\n";
        var newText = "one\nTWO\nthree\n";
        var patchPath = _workspace.File(PatchBetween(oldText, newText), ".patch");
        var sourcePath = _workspace.File(oldText);
        var outPath = _workspace.MissingPath();

        var exitCode = Run("apply-patch", sourcePath, patchPath, "--out", outPath);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Equal(["one", "TWO", "three"], File.ReadAllLines(outPath));
        Assert.Contains("wrote", Out);
    }

    // AC-010 Export Safety.
    [Fact]
    public void ApplyPatch_WithExistingOutFileAndNoForce_RefusesToOverwrite()
    {
        var oldText = "one\ntwo\n";
        var patchPath = _workspace.File(PatchBetween(oldText, "one\nTWO\n"), ".patch");
        var sourcePath = _workspace.File(oldText);
        var outPath = _workspace.File("precious\n");

        var exitCode = Run("apply-patch", sourcePath, patchPath, "--out", outPath);

        Assert.Equal(ExitCode.InvalidInput, exitCode);
        Assert.Contains("--force", Err);
        Assert.Equal("precious\n", File.ReadAllText(outPath));
    }

    [Fact]
    public void ApplyPatch_WithExistingOutFileAndForce_Overwrites()
    {
        var oldText = "one\ntwo\n";
        var patchPath = _workspace.File(PatchBetween(oldText, "one\nTWO\n"), ".patch");
        var sourcePath = _workspace.File(oldText);
        var outPath = _workspace.File("precious\n");

        var exitCode = Run("apply-patch", sourcePath, patchPath, "--out", outPath, "--force");

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Equal(["one", "TWO"], File.ReadAllLines(outPath));
    }

    // AC-005 Fuzzy Patch Reconstruction: the recorded offset is stale but the
    // context is still recognizable further down the file.
    [Fact]
    public void ApplyPatch_FuzzyMode_AppliesWhenTheHunkOffsetIsStale()
    {
        var patchPath = _workspace.File(PatchBetween("one\ntwo\nthree\n", "one\nTWO\nthree\n"), ".patch");
        var sourcePath = _workspace.File("header\nheader\none\ntwo\nthree\n");

        var exitCode = Run("apply-patch", sourcePath, patchPath, "--patch-mode", "fuzzy");

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Contains("TWO", Out);
        Assert.Contains("applied fuzzily", Err);
    }

    [Fact]
    public void ApplyPatch_ExactMode_RejectsAStaleOffsetTheFuzzyModeWouldAccept()
    {
        var patchPath = _workspace.File(PatchBetween("one\ntwo\nthree\n", "one\nTWO\nthree\n"), ".patch");
        var sourcePath = _workspace.File("header\nheader\none\ntwo\nthree\n");

        var exitCode = Run("apply-patch", sourcePath, patchPath);

        Assert.Equal(ExitCode.Conflicts, exitCode);
        Assert.Equal(string.Empty, Out);
    }

    [Fact]
    public void ApplyPatch_SemanticMode_AppliesByBlockIdentity()
    {
        // Semantic placement matches on block identity, so the hunk needs lines
        // distinctive enough to be anchors (AnchorDetector) — unlike fuzzy mode,
        // which only re-searches for the recorded context nearby.
        var oldText = "public void Run()\n{\n    Console.WriteLine(\"starting the process\");\n}\n";
        var newText = "public void Run()\n{\n    Console.WriteLine(\"beginning the process\");\n}\n";
        var patchPath = _workspace.File(PatchBetween(oldText, newText), ".patch");
        var sourcePath = _workspace.File("using System;\nnamespace Demo;\n\n" + oldText);

        var exitCode = Run("apply-patch", sourcePath, patchPath, "--patch-mode", "semantic");

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Contains("beginning the process", Out);
        Assert.Contains("applied semantically", Err);
    }

    [Fact]
    public void ApplyPatch_ContextMismatch_ExitsWithConflictsAndFriendlyError()
    {
        var header = new UnifiedDiffFileHeader("old.txt", "new.txt");
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 1, 1, 1),
            [new UnifiedDiffLine(UnifiedDiffLineKind.Context, "this-does-not-match-source")]);
        var patchPath = _workspace.File(
            string.Join("\n", UnifiedDiffFormatter.Format(new UnifiedDiffFile(header, [hunk]))) + "\n", ".patch");
        var sourcePath = _workspace.File("one\n");

        var exitCode = Run("apply-patch", sourcePath, patchPath);

        Assert.Equal(ExitCode.Conflicts, exitCode);
        Assert.Contains("does not match source", Err);
        Assert.DoesNotContain("StackTrace", Err);
        Assert.Equal(string.Empty, Out);
    }

    [Fact]
    public void ApplyPatch_MissingPatchFile_ExitsWithInvalidInput()
    {
        var sourcePath = _workspace.File("one\n");
        var missingPatchPath = _workspace.MissingPath(".patch");

        var exitCode = Run("apply-patch", sourcePath, missingPatchPath);

        Assert.Equal(ExitCode.InvalidInput, exitCode);
        Assert.Contains(missingPatchPath, Err);
        Assert.DoesNotContain("StackTrace", Err);
    }

    [Fact]
    public void ApplyPatch_EmptyPatchFile_ExitsWithInvalidInput()
    {
        var sourcePath = _workspace.File("one\n");
        var emptyPatchPath = _workspace.File(string.Empty, ".patch");

        var exitCode = Run("apply-patch", sourcePath, emptyPatchPath);

        Assert.Equal(ExitCode.InvalidInput, exitCode);
        Assert.Contains("no file entries", Err);
        Assert.Equal(string.Empty, Out);
    }

    [Fact]
    public void ExportPatch_TwoFiles_WritesUnifiedDiffToStdout()
    {
        var oldPath = _workspace.File("one\ntwo\nthree\n");
        var newPath = _workspace.File("one\nTWO\nthree\n");

        var exitCode = Run("export-patch", oldPath, newPath);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Contains("@@", Out);
        Assert.Contains("-two", Out);
        Assert.Contains("+TWO", Out);
    }

    [Fact]
    public void ExportPatch_IdenticalFiles_ExitsWithNoDifferences()
    {
        var oldPath = _workspace.File("one\n");
        var newPath = _workspace.File("one\n");

        Assert.Equal(ExitCode.NoDifferences, Run("export-patch", oldPath, newPath));
    }

    [Fact]
    public void ExportPatch_SvnFormat_EmitsSvnStyleHeaders()
    {
        var oldPath = _workspace.File("one\ntwo\n");
        var newPath = _workspace.File("one\nTWO\n");

        Run("export-patch", oldPath, newPath, "--format", "svn");

        Assert.Contains("Index:", Out);
    }

    [Fact]
    public void ExportPatch_ToFile_RoundTripsThroughApplyPatch()
    {
        var oldPath = _workspace.File("one\ntwo\nthree\n");
        var newPath = _workspace.File("one\nTWO\nthree\n");
        var patchPath = _workspace.MissingPath(".patch");

        Run("export-patch", oldPath, newPath, "--out", patchPath);

        var applyOutput = new StringWriter();
        var applyError = new StringWriter();
        var exitCode = CliRunner.Run(["apply-patch", oldPath, patchPath], applyOutput, applyError);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Equal(File.ReadAllText(newPath).Replace("\n", Environment.NewLine), applyOutput.ToString());
    }

    [Fact]
    public void ExportPatch_WithExistingOutFileAndNoForce_RefusesToOverwrite()
    {
        var oldPath = _workspace.File("one\n");
        var newPath = _workspace.File("two\n");
        var patchPath = _workspace.File("precious\n", ".patch");

        var exitCode = Run("export-patch", oldPath, newPath, "--out", patchPath);

        Assert.Equal(ExitCode.InvalidInput, exitCode);
        Assert.Equal("precious\n", File.ReadAllText(patchPath));
    }

    private static string PatchBetween(string oldText, string newText)
    {
        var result = FileComparer.Compare(Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText));
        var file = UnifiedDiffBuilder.Build(result.Changes, "old.txt", "new.txt");
        return string.Join("\n", UnifiedDiffFormatter.Format(file)) + "\n";
    }
}
