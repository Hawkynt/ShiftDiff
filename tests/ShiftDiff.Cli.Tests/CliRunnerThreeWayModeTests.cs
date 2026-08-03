using ShiftDiff.Cli;
using Xunit;

namespace ShiftDiff.Cli.Tests;

public class CliRunnerThreeWayModeTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public void Dispose() => _workspace.Dispose();

    private string Out => _output.ToString();

    private string Err => _error.ToString();

    private int Run(params string[] args) => CliRunner.Run(args, _output, _error);

    [Fact]
    public void Compare3_NoConflicts_MergesCleanlyAndReportsDifferences()
    {
        var basePath = _workspace.File("one\ntwo\nthree\n");
        var localPath = _workspace.File("one\nTWO\nthree\n");
        var remotePath = _workspace.File("one\ntwo\nthree\n");

        var exitCode = Run(basePath, localPath, remotePath);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Equal($"one{Environment.NewLine}TWO{Environment.NewLine}three{Environment.NewLine}", Out);
        Assert.DoesNotContain("<<<<<<<", Out);
        Assert.Equal(string.Empty, Err);
    }

    [Fact]
    public void Compare3_IdenticalSides_ExitsWithNoDifferences()
    {
        var basePath = _workspace.File("one\ntwo\n");
        var localPath = _workspace.File("one\ntwo\n");
        var remotePath = _workspace.File("one\ntwo\n");

        Assert.Equal(ExitCode.NoDifferences, Run("compare3", basePath, localPath, remotePath));
    }

    [Fact]
    public void Compare3_WithConflict_EmitsGitStyleMarkersAndExitsWithConflictCode()
    {
        var basePath = _workspace.File("one\ntwo\nthree\n");
        var localPath = _workspace.File("one\nTWO-local\nthree\n");
        var remotePath = _workspace.File("one\ntwo-remote\nthree\n");

        var exitCode = Run(basePath, localPath, remotePath);

        Assert.Equal(ExitCode.Conflicts, exitCode);
        Assert.Contains("<<<<<<< local", Out);
        Assert.Contains("TWO-local", Out);
        Assert.Contains("=======", Out);
        Assert.Contains("two-remote", Out);
        Assert.Contains(">>>>>>> remote", Out);
        Assert.Contains("1 conflict", Err);
    }

    [Fact]
    public void Compare3_ConsecutiveConflictLines_GroupedIntoOneMarkerBlock()
    {
        var basePath = _workspace.File("a\nb\nc\nd\n");
        var localPath = _workspace.File("a\nB-local\nC-local\nd\n");
        var remotePath = _workspace.File("a\nB-remote\nC-remote\nd\n");

        var exitCode = Run(basePath, localPath, remotePath);

        Assert.Equal(ExitCode.Conflicts, exitCode);
        Assert.Equal(1, CountOccurrences(Out, "<<<<<<< local"));
        Assert.Equal(1, CountOccurrences(Out, ">>>>>>> remote"));
        Assert.Contains($"B-local{Environment.NewLine}C-local{Environment.NewLine}=======", Out);
        Assert.Contains($"B-remote{Environment.NewLine}C-remote{Environment.NewLine}>>>>>>> remote", Out);
    }

    [Fact]
    public void Compare3_BothSidesDeleteSameLine_DropsLineFromResolvedOutput()
    {
        var basePath = _workspace.File("a\nb\nc\n");
        var localPath = _workspace.File("a\nc\n");
        var remotePath = _workspace.File("a\nc\n");

        var exitCode = Run(basePath, localPath, remotePath);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Equal($"a{Environment.NewLine}c{Environment.NewLine}", Out);
        Assert.Equal(string.Empty, Err);
    }

    [Fact]
    public void Compare3_JsonFormat_ReportsConflictCountAndMergedLines()
    {
        var basePath = _workspace.File("one\ntwo\n");
        var localPath = _workspace.File("one\nTWO-local\n");
        var remotePath = _workspace.File("one\ntwo-remote\n");

        var exitCode = Run("compare3", basePath, localPath, remotePath, "--json");

        Assert.Equal(ExitCode.Conflicts, exitCode);
        using var document = System.Text.Json.JsonDocument.Parse(Out);
        Assert.Equal(1, document.RootElement.GetProperty("conflicts").GetInt32());
        Assert.NotEmpty(document.RootElement.GetProperty("merged").EnumerateArray());
    }

    [Fact]
    public void Compare3_MissingFile_ExitsWithInvalidInput()
    {
        var localPath = _workspace.File("one\n");
        var remotePath = _workspace.File("one\n");
        var missingBasePath = _workspace.MissingPath();

        var exitCode = Run(missingBasePath, localPath, remotePath);

        Assert.Equal(ExitCode.InvalidInput, exitCode);
        Assert.Contains(missingBasePath, Err);
        Assert.DoesNotContain("StackTrace", Err);
        Assert.Equal(string.Empty, Out);
    }

    // AC-003 / FR-023: the fourth file is the candidate reconstruction that gets
    // validated against the merge of the first three.
    [Fact]
    public void Compare4_TargetMatchesTheMergeResult_ExitsWithNoDifferences()
    {
        var basePath = _workspace.File("one\ntwo\nthree\n");
        var localPath = _workspace.File("one\nTWO\nthree\n");
        var remotePath = _workspace.File("one\ntwo\nthree\n");
        var targetPath = _workspace.File("one\nTWO\nthree\n");

        var exitCode = Run(basePath, localPath, remotePath, targetPath);

        Assert.Equal(ExitCode.NoDifferences, exitCode);
        Assert.Contains("target matches", Out);
    }

    [Fact]
    public void Compare4_TargetDiffersFromTheMergeResult_ReportsTheDiscrepancies()
    {
        var basePath = _workspace.File("one\ntwo\nthree\n");
        var localPath = _workspace.File("one\nTWO\nthree\n");
        var remotePath = _workspace.File("one\ntwo\nthree\n");
        var targetPath = _workspace.File("one\nsomething-else\nthree\n");

        var exitCode = Run(basePath, localPath, remotePath, targetPath);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Contains("target differs", Out);
        Assert.Contains("something-else", Out);
    }

    [Fact]
    public void Compare4_WithConflictingSides_ExitsWithConflictCode()
    {
        var basePath = _workspace.File("one\ntwo\n");
        var localPath = _workspace.File("one\nTWO-local\n");
        var remotePath = _workspace.File("one\ntwo-remote\n");
        var targetPath = _workspace.File("one\nresolved\n");

        var exitCode = Run("compare4", basePath, localPath, remotePath, targetPath);

        Assert.Equal(ExitCode.Conflicts, exitCode);
        Assert.Contains("conflict", Err);
    }

    private static int CountOccurrences(string text, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }
}
