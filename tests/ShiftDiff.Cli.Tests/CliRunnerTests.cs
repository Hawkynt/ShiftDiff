using ShiftDiff.Cli;
using Xunit;

namespace ShiftDiff.Cli.Tests;

public class CliRunnerTests
{
    [Fact]
    public void Run_TwoIdenticalFiles_ReturnsZeroAndPrintsHeaderWithNoHunks()
    {
        var oldPath = WriteTempFile("one\ntwo\nthree\n");
        var newPath = WriteTempFile("one\ntwo\nthree\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { oldPath, newPath }, output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains($"--- {oldPath}", output.ToString());
        Assert.Contains($"+++ {newPath}", output.ToString());
        Assert.DoesNotContain("@@", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_TwoDifferentFiles_PrintsUnifiedDiffHunk()
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

    [Fact]
    public void Run_TwoIniFiles_PrintsFormattedIniChangesNotUnifiedDiff()
    {
        var oldPath = WriteTempFile("[a]\nkey=1\nother=2\n", ".ini");
        var newPath = WriteTempFile("[a]\nkey=1\nother=3\n", ".ini");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { oldPath, newPath }, output, error);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("a.other: Changed 2 -> 3", text);
        Assert.DoesNotContain("a.key", text);
        Assert.DoesNotContain("@@", text);
        Assert.DoesNotContain("---", text);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_TwoJsonFiles_PrintsFormattedJsonChangesNotUnifiedDiff()
    {
        var oldPath = WriteTempFile("{\"a\":{\"key\":1,\"other\":2}}", ".json");
        var newPath = WriteTempFile("{\"a\":{\"key\":1,\"other\":3}}", ".json");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(new[] { oldPath, newPath }, output, error);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("a.other: Changed 2 -> 3", text);
        Assert.DoesNotContain("a.key", text);
        Assert.DoesNotContain("@@", text);
        Assert.DoesNotContain("---", text);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_NoArgs_ReturnsNonZeroAndWritesUsageToError()
    {
        AssertWrongArgCountFails(Array.Empty<string>());
    }

    [Fact]
    public void Run_OneArg_ReturnsNonZeroAndWritesUsageToError()
    {
        AssertWrongArgCountFails(new[] { "only-one-arg" });
    }

    private static void AssertWrongArgCountFails(string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliRunner.Run(args, output, error);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("usage", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Run_MissingFile_ReturnsNonZeroWithFriendlyErrorNotStackTrace()
    {
        var newPath = WriteTempFile("one\n");
        var output = new StringWriter();
        var error = new StringWriter();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");

        var exitCode = CliRunner.Run(new[] { missingPath, newPath }, output, error);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(missingPath, error.ToString());
        Assert.DoesNotContain("StackTrace", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Run_UnreadableFile_ReturnsNonZeroWithFriendlyErrorNotUnhandledException()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var newPath = WriteTempFile("one\n");
        var unreadablePath = WriteTempFile("two\n");
        File.SetUnixFileMode(unreadablePath, UnixFileMode.None);
        var output = new StringWriter();
        var error = new StringWriter();

        try
        {
            var exitCode = CliRunner.Run(new[] { unreadablePath, newPath }, output, error);

            Assert.NotEqual(0, exitCode);
            Assert.Contains(unreadablePath, error.ToString());
            Assert.DoesNotContain("StackTrace", error.ToString());
            Assert.Equal(string.Empty, output.ToString());
        }
        finally
        {
            File.SetUnixFileMode(unreadablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static string WriteTempFile(string content) => WriteTempFile(content, ".txt");

    private static string WriteTempFile(string content, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, content);
        return path;
    }
}
