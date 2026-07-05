using ShiftDiff.Cli;
using Xunit;

namespace ShiftDiff.Cli.Tests;

public class CliRunnerThreeWayModeTests
{
    [Fact]
    public void Run_ThreeWayMode_NoConflicts_MergesCleanlyWithZeroExit()
    {
        var basePath = WriteTempFile("one\ntwo\nthree\n");
        var localPath = WriteTempFile("one\nTWO\nthree\n");
        var remotePath = WriteTempFile("one\ntwo\nthree\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { basePath, localPath, remotePath }, output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            $"one{Environment.NewLine}TWO{Environment.NewLine}three{Environment.NewLine}",
            output.ToString());
        Assert.DoesNotContain("<<<<<<<", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_ThreeWayMode_WithConflict_EmitsGitStyleMarkersAndNonZeroExit()
    {
        var basePath = WriteTempFile("one\ntwo\nthree\n");
        var localPath = WriteTempFile("one\nTWO-local\nthree\n");
        var remotePath = WriteTempFile("one\ntwo-remote\nthree\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { basePath, localPath, remotePath }, output, error);

        Assert.NotEqual(0, exitCode);
        var text = output.ToString();
        Assert.Contains("<<<<<<< local", text);
        Assert.Contains("TWO-local", text);
        Assert.Contains("=======", text);
        Assert.Contains("two-remote", text);
        Assert.Contains(">>>>>>> remote", text);
        Assert.Contains("1 conflict", error.ToString());
    }

    [Fact]
    public void Run_ThreeWayMode_ConsecutiveConflictLines_GroupedIntoOneMarkerBlock()
    {
        var basePath = WriteTempFile("a\nb\nc\nd\n");
        var localPath = WriteTempFile("a\nB-local\nC-local\nd\n");
        var remotePath = WriteTempFile("a\nB-remote\nC-remote\nd\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { basePath, localPath, remotePath }, output, error);

        Assert.NotEqual(0, exitCode);
        var text = output.ToString();
        Assert.Equal(1, CountOccurrences(text, "<<<<<<< local"));
        Assert.Equal(1, CountOccurrences(text, ">>>>>>> remote"));
        Assert.Contains($"B-local{Environment.NewLine}C-local{Environment.NewLine}=======", text);
        Assert.Contains($"B-remote{Environment.NewLine}C-remote{Environment.NewLine}>>>>>>> remote", text);
    }

    [Fact]
    public void Run_ThreeWayMode_BothSidesDeleteSameLine_DropsLineFromResolvedOutputWithZeroExit()
    {
        var basePath = WriteTempFile("a\nb\nc\n");
        var localPath = WriteTempFile("a\nc\n");
        var remotePath = WriteTempFile("a\nc\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { basePath, localPath, remotePath }, output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal($"a{Environment.NewLine}c{Environment.NewLine}", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_ThreeWayMode_MissingFile_ReturnsNonZeroWithFriendlyError()
    {
        var localPath = WriteTempFile("one\n");
        var remotePath = WriteTempFile("one\n");
        var missingBasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { missingBasePath, localPath, remotePath }, output, error);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(missingBasePath, error.ToString());
        Assert.DoesNotContain("StackTrace", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
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

    private static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, content);
        return path;
    }
}
