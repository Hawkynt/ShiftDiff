using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

// FR-047 in merge mode: the result is the local file, and any pane's version of
// a conflicting block can replace it.
public class ShellThreeWayMergeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-3merge", Guid.NewGuid().ToString("N"));

    public ShellThreeWayMergeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ThreeWaySession_StartsTheResultFromTheLocalFile()
    {
        var shell = await OpenThreeWayAsync("one\ntwo\n", "one\nLOCAL\n", "one\nremote\n");

        Assert.True(shell.CanResolve);
        Assert.Equal(["one", "LOCAL"], shell.MergedLines);
    }

    [Fact]
    public async Task TakingTheRemotePane_ReplacesTheConflictingLine()
    {
        var shell = await OpenThreeWayAsync("one\ntwo\n", "one\nLOCAL\n", "one\nremote\n");
        shell.GoToNextConflict();

        Assert.True(shell.TakeSelectedBlock(2));
        Assert.Equal(["one", "remote"], shell.MergedLines);
    }

    [Fact]
    public async Task TakingTheBasePane_RestoresTheOriginalLine()
    {
        var shell = await OpenThreeWayAsync("one\ntwo\n", "one\nLOCAL\n", "one\nremote\n");
        shell.GoToNextConflict();

        Assert.True(shell.TakeSelectedBlock(0));
        Assert.Equal(["one", "two"], shell.MergedLines);
    }

    [Fact]
    public async Task UndoingAResolution_RestoresTheLocalVersion()
    {
        var shell = await OpenThreeWayAsync("one\ntwo\n", "one\nLOCAL\n", "one\nremote\n");
        shell.GoToNextConflict();
        shell.TakeSelectedBlock(2);

        Assert.True(shell.UndoMerge());
        Assert.Equal(["one", "LOCAL"], shell.MergedLines);
    }

    [Fact]
    public async Task TakingAPaneThatDoesNotExist_DoesNothing()
    {
        var shell = await OpenThreeWayAsync("one\ntwo\n", "one\nLOCAL\n", "one\nremote\n");
        shell.GoToNextConflict();

        Assert.False(shell.TakeSelectedBlock(9));
        Assert.False(shell.TakeSelectedBlock(-1));
    }

    [Fact]
    public async Task ResolvedResult_CanBeSaved()
    {
        var shell = await OpenThreeWayAsync("one\ntwo\n", "one\nLOCAL\n", "one\nremote\n");
        shell.GoToNextConflict();
        shell.TakeSelectedBlock(2);
        var path = Path.Combine(_root, "resolved.txt");

        Assert.True(shell.SaveMergedResult(path));
        Assert.Equal(["one", "remote"], File.ReadAllLines(path));
    }

    // The fourth pane is a candidate to validate, not a target to edit.
    [Fact]
    public async Task FourWaySession_OffersNoResolution()
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenFourWayAsync(
            Write("base.txt", "one\ntwo\n"),
            Write("local.txt", "one\nLOCAL\n"),
            Write("remote.txt", "one\nremote\n"),
            Write("target.txt", "one\nLOCAL\n"));

        Assert.False(shell.CanResolve);
        Assert.False(shell.TakeSelectedBlock(0));
    }

    [Fact]
    public async Task TwoWaySession_StillResolvesFromTheLeftPane()
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "one\ntwo\n", "one\nTWO\n"));
        shell.GoToNextChange();

        Assert.True(shell.TakeSelectedBlockFromLeft());
        Assert.Equal(["one", "two"], shell.MergedLines);
    }

    private async Task<ShellViewModel> OpenThreeWayAsync(string baseText, string localText, string remoteText)
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenThreeWayAsync(
            Write("base.txt", baseText), Write("local.txt", localText), Write("remote.txt", remoteText));
        return shell;
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }
}
