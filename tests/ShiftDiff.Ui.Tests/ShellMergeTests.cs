using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

// FR-047 Inline Editing: taking a block from the source side into the
// reconstructed target, with undo and a guarded save (AC-010).
public class ShellMergeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-merge", Guid.NewGuid().ToString("N"));

    public ShellMergeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task MergedLines_StartOutAsTheTargetFile()
    {
        var shell = await OpenAsync("one\ntwo\n", "one\nTWO\n");

        Assert.Equal(["one", "TWO"], shell.MergedLines);
        Assert.False(shell.CanUndoMerge);
    }

    [Fact]
    public async Task TakeSelectedBlockFromLeft_ReplacesTheEditedLineWithTheSourceVersion()
    {
        var shell = await OpenAsync("one\ntwo\n", "one\nTWO\n");
        shell.GoToNextChange();

        Assert.True(shell.TakeSelectedBlockFromLeft());
        Assert.Equal(["one", "two"], shell.MergedLines);
        Assert.True(shell.CanUndoMerge);
    }

    [Fact]
    public async Task TakeSelectedBlockFromLeft_RestoresAWholeRemovedBlock()
    {
        var shell = await OpenAsync("keep\ngone one\ngone two\ntail\n", "keep\ntail\n");
        shell.GoToNextChange();

        Assert.True(shell.TakeSelectedBlockFromLeft());
        Assert.Contains("gone one", shell.MergedLines);
        Assert.Contains("gone two", shell.MergedLines);
    }

    [Fact]
    public async Task TakeSelectedBlockFromLeft_DropsALineThatOnlyExistsInTheTarget()
    {
        var shell = await OpenAsync("keep\n", "keep\nextra\n");
        shell.GoToNextChange();

        Assert.True(shell.TakeSelectedBlockFromLeft());
        Assert.DoesNotContain("extra", shell.MergedLines);
    }

    [Fact]
    public async Task TakeSelectedBlockFromLeft_WithoutASelectedChange_DoesNothing()
    {
        var shell = await OpenAsync("one\n", "one\n");

        Assert.False(shell.TakeSelectedBlockFromLeft());
    }

    [Fact]
    public async Task UndoMerge_RestoresThePreviousMergedContent()
    {
        var shell = await OpenAsync("one\ntwo\n", "one\nTWO\n");
        shell.GoToNextChange();
        shell.TakeSelectedBlockFromLeft();

        Assert.True(shell.UndoMerge());
        Assert.Equal(["one", "TWO"], shell.MergedLines);
        Assert.False(shell.CanUndoMerge);
    }

    [Fact]
    public async Task UndoMerge_WithNothingToUndo_ReturnsFalse()
    {
        var shell = await OpenAsync("one\n", "one\n");

        Assert.False(shell.UndoMerge());
    }

    [Fact]
    public async Task SaveMergedResult_WritesTheReconstructedTarget()
    {
        var shell = await OpenAsync("one\ntwo\n", "one\nTWO\n");
        var path = Path.Combine(_root, "resolved.txt");

        Assert.True(shell.SaveMergedResult(path));
        Assert.Equal(["one", "TWO"], File.ReadAllLines(path));
    }

    // AC-010 Export Safety.
    [Fact]
    public async Task SaveMergedResult_OverAnExistingFile_RefusesUnlessOverwriteIsRequested()
    {
        var shell = await OpenAsync("one\n", "ONE\n");
        var path = Path.Combine(_root, "existing.txt");
        File.WriteAllText(path, "precious\n");

        Assert.False(shell.SaveMergedResult(path));
        Assert.Equal("precious\n", File.ReadAllText(path));

        Assert.True(shell.SaveMergedResult(path, overwrite: true));
        Assert.Equal(["ONE"], File.ReadAllLines(path));
    }

    [Fact]
    public async Task OpeningAnotherFile_StartsAFreshMergeDocument()
    {
        var shell = await OpenAsync("one\ntwo\n", "one\nTWO\n");
        shell.GoToNextChange();
        shell.TakeSelectedBlockFromLeft();

        await shell.OpenAsync(InMemoryComparisonSource.FromText("other.txt", "a\n", "b\n"));

        Assert.Equal(["b"], shell.MergedLines);
        Assert.False(shell.CanUndoMerge);
    }

    private static async Task<ShellViewModel> OpenAsync(string oldText, string newText)
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("file.txt", oldText, newText));
        return shell;
    }
}
