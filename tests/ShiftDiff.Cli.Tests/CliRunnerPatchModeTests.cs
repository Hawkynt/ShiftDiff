using System.Text;
using ShiftDiff.Cli;
using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Cli.Tests;

public class CliRunnerPatchModeTests
{
    [Fact]
    public void Run_PatchMode_CleanPatch_AppliesAndPrintsResult()
    {
        var oldText = "one\ntwo\nthree\n";
        var newText = "one\nTWO\nthree\n";

        var result = FileComparer.Compare(Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText));
        var unifiedDiffFile = UnifiedDiffBuilder.Build(result.Changes, "old.txt", "new.txt");
        var patchLines = UnifiedDiffFormatter.Format(unifiedDiffFile);

        var patchPath = WriteTempFile(string.Join("\n", patchLines) + "\n");
        var sourcePath = WriteTempFile(oldText);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { "--patch", patchPath, "--source", sourcePath }, output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(newText.Replace("\n", Environment.NewLine), output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_PatchMode_ContextMismatch_ReturnsNonZeroWithFriendlyError()
    {
        var header = new UnifiedDiffFileHeader("old.txt", "new.txt");
        var hunkHeader = new UnifiedDiffHunkHeader(1, 1, 1, 1);
        var hunkLines = new[]
        {
            new UnifiedDiffLine(UnifiedDiffLineKind.Context, "this-does-not-match-source"),
        };
        var hunk = new UnifiedDiffHunk(hunkHeader, hunkLines);
        var file = new UnifiedDiffFile(header, new[] { hunk });
        var patchLines = UnifiedDiffFormatter.Format(file);

        var patchPath = WriteTempFile(string.Join("\n", patchLines) + "\n");
        var sourcePath = WriteTempFile("one\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { "--patch", patchPath, "--source", sourcePath }, output, error);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("does not match source", error.ToString());
        Assert.DoesNotContain("StackTrace", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Run_PatchMode_MissingPatchFile_ReturnsNonZeroWithFriendlyError()
    {
        var sourcePath = WriteTempFile("one\n");
        var missingPatchPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".patch");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { "--patch", missingPatchPath, "--source", sourcePath }, output, error);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(missingPatchPath, error.ToString());
        Assert.DoesNotContain("StackTrace", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Run_TwoArgMode_StillWorksUnchanged()
    {
        var oldPath = WriteTempFile("one\ntwo\nthree\n");
        var newPath = WriteTempFile("one\nTWO\nthree\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { oldPath, newPath }, output, error);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("@@", text);
        Assert.Contains("-two", text);
        Assert.Contains("+TWO", text);
    }

    private static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, content);
        return path;
    }
}
