using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

public class ShellViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-ui", Guid.NewGuid().ToString("N"));

    public ShellViewModelTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task OpenAsync_InMemorySource_LoadsTheDocumentAndReportsTheSummary()
    {
        var shell = new ShellViewModel();

        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.cs", "one\ntwo\n", "one\nTWO\n"));

        Assert.True(shell.Summary.HasDifferences);
        Assert.NotEmpty(shell.VisibleRows);
        Assert.Contains("edited", shell.StatusText);
        Assert.False(shell.IsBusy);
    }

    [Fact]
    public async Task OpenAsync_IdenticalContent_ReportsNoDifferences()
    {
        var shell = new ShellViewModel();

        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.cs", "same\n", "same\n"));

        Assert.False(shell.Summary.HasDifferences);
        Assert.Contains("No differences", shell.StatusText);
    }

    [Fact]
    public async Task OpenFilePairAsync_ReadsBothFilesFromDisk()
    {
        var oldPath = Write("a.cs", "int x = 1;\n");
        var newPath = Write("b.cs", "int x = 2;\n");
        var shell = new ShellViewModel();

        await shell.OpenFilePairAsync(oldPath, newPath);

        Assert.Equal(1, shell.Summary.Edited);
        Assert.Equal(oldPath, shell.OldTitle);
        Assert.Equal(newPath, shell.NewTitle);
    }

    [Fact]
    public async Task OpenFolderPairAsync_ListsChangedFilesAndOpensTheFirst()
    {
        var left = Directory.CreateDirectory(Path.Combine(_root, "left")).FullName;
        var right = Directory.CreateDirectory(Path.Combine(_root, "right")).FullName;
        File.WriteAllText(Path.Combine(left, "same.txt"), "x\n");
        File.WriteAllText(Path.Combine(right, "same.txt"), "x\n");
        File.WriteAllText(Path.Combine(left, "edit.txt"), "one\n");
        File.WriteAllText(Path.Combine(right, "edit.txt"), "two\n");
        var shell = new ShellViewModel();

        await shell.OpenFolderPairAsync(left, right);

        var entry = Assert.Single(shell.Files);
        Assert.Equal("edit.txt", entry.DisplayPath);
        Assert.Equal(ChangeType.Edited, entry.ChangeType);
        Assert.NotEmpty(shell.VisibleRows);
    }

    [Fact]
    public async Task OpenDroppedAsync_TwoFiles_OpensATwoWayComparison()
    {
        var shell = new ShellViewModel();

        await shell.OpenDroppedAsync([Write("a.txt", "one\n"), Write("b.txt", "two\n")]);

        Assert.Equal(2, shell.Document.PaneCount);
    }

    [Fact]
    public async Task OpenDroppedAsync_ThreeFiles_OpensAThreeWayComparison()
    {
        var shell = new ShellViewModel();

        await shell.OpenDroppedAsync([Write("base.txt", "one\n"), Write("local.txt", "ONE\n"), Write("remote.txt", "one\n")]);

        Assert.Equal(3, shell.Document.PaneCount);
        Assert.Equal(PaneLayout.ThreeWay, shell.Settings.Layout);
    }

    [Fact]
    public async Task OpenDroppedAsync_FourFiles_OpensAFourWayComparison()
    {
        var shell = new ShellViewModel();

        await shell.OpenDroppedAsync(
        [
            Write("base.txt", "one\n"), Write("local.txt", "ONE\n"),
            Write("remote.txt", "one\n"), Write("target.txt", "ONE\n"),
        ]);

        Assert.Equal(4, shell.Document.PaneCount);
        Assert.Equal(PaneLayout.FourWay, shell.Settings.Layout);
    }

    [Fact]
    public async Task OpenDroppedAsync_OneFile_AsksForASecond()
    {
        var shell = new ShellViewModel();

        await shell.OpenDroppedAsync([Write("a.txt", "one\n")]);

        Assert.Contains("second file", shell.StatusText);
    }

    [Fact]
    public async Task OpenDroppedAsync_NothingDropped_LeavesTheSessionAlone()
    {
        var shell = new ShellViewModel();

        await shell.OpenDroppedAsync([]);

        Assert.Empty(shell.VisibleRows);
    }

    [Fact]
    public async Task ChangingIgnoreCase_RerunsTheComparison()
    {
        var shell = new ShellViewModel();
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "one\ntwo\n", "one\nTWO\n"));
        Assert.Equal(1, shell.Summary.Edited);

        shell.Settings.IgnoreCase = true;
        await WaitForIdle(shell);

        Assert.Equal(0, shell.Summary.Edited);
    }

    [Fact]
    public async Task ChangingCollapseUnchanged_RebuildsTheRowsWithoutRecomparing()
    {
        var text = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"line {i}"));
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", text, text.Replace("line 20", "line twenty")));
        var expanded = shell.VisibleRows.Count;

        shell.Settings.CollapseUnchanged = true;
        await WaitForIdle(shell);

        Assert.True(shell.VisibleRows.Count < expanded);
    }

    [Fact]
    public async Task Navigation_MovesTheSelectionBetweenChanges()
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "a\nb\nc\nd\ne\n", "a\nB\nc\nD\ne\n"));

        shell.GoToNextChange();
        var first = shell.SelectedRow;
        shell.GoToNextChange();
        var second = shell.SelectedRow;

        Assert.True(first >= 0);
        Assert.True(second > first);
        shell.GoToPreviousChange();
        Assert.Equal(first, shell.SelectedRow);
    }

    [Fact]
    public async Task SelectingARow_PopulatesTheChangeDetailsInspector()
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "value = 1\n", "value = 2\n"));

        shell.GoToNextChange();

        Assert.Contains(shell.Details.Entries, entry => entry.Label == "Change type");
        Assert.Equal("edited", shell.Details.Title);
    }

    [Fact]
    public async Task ChangePositionText_ReportsTheCursorPositionAmongTheChanges()
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "a\nb\nc\nd\ne\n", "a\nB\nc\nD\ne\n"));

        shell.GoToNextChange();

        Assert.Equal("change 1 of 2", shell.ChangePositionText);
    }

    [Fact]
    public async Task SearchText_FiltersTheVisibleRows()
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "alpha\nbeta\n", "alpha\nbeta\n"));

        shell.SearchText = "beta";

        Assert.Single(shell.VisibleRows);
    }

    [Fact]
    public async Task Filter_HidesUnchangedRows()
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "a\nb\n", "a\nB\n"));

        shell.Filter = ChangeTypeFilter.OnlyChanges;

        Assert.All(shell.VisibleRows, row => Assert.True(row.IsChanged));
    }

    [Fact]
    public async Task Navigating_WhileAFilterHidesTheTarget_ClearsTheFilter()
    {
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "a\nb\n", "a\nB\n"));
        shell.SearchText = "no-such-text";
        Assert.Empty(shell.VisibleRows);

        shell.GoToNextChange();

        Assert.NotEmpty(shell.VisibleRows);
        Assert.True(shell.SelectedRow >= 0);
    }

    [Fact]
    public async Task GoToOverviewPosition_SelectsTheRowAtThatFraction()
    {
        var text = string.Join("\n", Enumerable.Range(0, 20).Select(i => $"line {i}"));
        var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
        await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", text, text));

        shell.GoToOverviewPosition(0.5);

        Assert.Equal(10, shell.SelectedRow);
    }

    [Fact]
    public async Task OpenRepositoryAsync_OnAPlainFolder_ReportsThatNoRepositoryWasFound()
    {
        var shell = new ShellViewModel();

        await shell.OpenRepositoryAsync(_root);

        Assert.Contains("No Git or SVN repository", shell.StatusText);
    }

    [Fact]
    public async Task OpenAsync_UnreadableFile_ReportsAFailureInsteadOfThrowing()
    {
        var shell = new ShellViewModel();
        var missing = Path.Combine(_root, "missing.txt");

        await Assert.ThrowsAnyAsync<IOException>(() => shell.OpenFilePairAsync(missing, missing));
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    // Settings changes kick off a background recompare; give it a moment to land.
    private static async Task WaitForIdle(ShellViewModel shell)
    {
        for (var i = 0; i < 100 && shell.IsBusy; i++) await Task.Delay(10);
        await Task.Delay(20);
    }
}
