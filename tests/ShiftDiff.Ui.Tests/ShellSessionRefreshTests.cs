using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

// A setting has to affect whatever session is open — a three-way or four-way
// comparison must react the same way a two-way one does.
public class ShellSessionRefreshTests : IDisposable {
  private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-refresh", Guid.NewGuid().ToString("N"));

  public ShellSessionRefreshTests() => Directory.CreateDirectory(_root);

  public void Dispose() {
    if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
  }

  [Fact]
  public async Task ThreeWaySession_TogglingFolding_RebuildsTheRows() {
    var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
    var body = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"line {i}"));
    await shell.OpenThreeWayAsync(
        Write("base.txt", body),
        Write("local.txt", body.Replace("line 20", "LOCAL")),
        Write("remote.txt", body));
    var expanded = shell.VisibleRows.Count;

    shell.Settings.CollapseUnchanged = true;
    await WaitForIdle(shell);

    Assert.True(shell.VisibleRows.Count < expanded);
  }

  [Fact]
  public async Task ThreeWaySession_TogglingIgnoreCase_RecomparesTheSources() {
    var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
    await shell.OpenThreeWayAsync(
        Write("base.txt", "one\ntwo\n"),
        Write("local.txt", "one\nTWO\n"),
        Write("remote.txt", "one\ntwo\n"));
    Assert.Contains(shell.VisibleRows, row => row.IsChanged);

    shell.Settings.IgnoreCase = true;
    await WaitForIdle(shell);

    Assert.DoesNotContain(shell.VisibleRows, row => row.IsChanged);
  }

  [Fact]
  public async Task FourWaySession_TogglingASetting_KeepsTheFourPanes() {
    var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
    await shell.OpenFourWayAsync(
        Write("base.txt", "one\ntwo\n"),
        Write("local.txt", "one\nTWO\n"),
        Write("remote.txt", "one\ntwo\n"),
        Write("target.txt", "one\nTWO\n"));

    shell.Settings.CollapseUnchanged = true;
    await WaitForIdle(shell);

    Assert.Equal(4, shell.Document.PaneCount);
  }

  [Fact]
  public async Task SwitchingToTheUnifiedLayout_ReprojectsAnOpenComparison() {
    var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
    await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "one\ntwo\n", "one\nTWO\n"));
    Assert.Equal(2, shell.Document.PaneCount);

    shell.Settings.Layout = PaneLayout.Unified;
    await WaitForIdle(shell);

    Assert.Equal(1, shell.Document.PaneCount);
  }

  [Fact]
  public async Task ExpandRegion_RevealsOnlyThatFoldedRun() {
    var body = string.Join("\n", Enumerable.Range(0, 120).Select(i => $"line {i}"));
    var changed = body.Replace("line 10", "LINE TEN").Replace("line 100", "LINE HUNDRED");
    var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = true, ContextLines = 2 });
    await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", body, changed));

    var folded = shell.Document.Rows.Count(row => row.IsCollapsed);
    Assert.True(folded >= 2);

    await shell.ExpandRegionAsync(shell.Document.Rows.First(row => row.IsCollapsed));

    Assert.True(shell.Settings.CollapseUnchanged, "folding stays on for the rest of the document");
    Assert.Equal(folded - 1, shell.Document.Rows.Count(row => row.IsCollapsed));
  }

  [Fact]
  public async Task ExpandRegion_RestoresTheHiddenLines() {
    var body = string.Join("\n", Enumerable.Range(0, 60).Select(i => $"line {i}"));
    var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = true, ContextLines = 2 });
    await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", body, body.Replace("line 30", "CHANGED")));

    var before = shell.Document.Rows.Count;
    var hidden = shell.Document.Rows.First(row => row.IsCollapsed).HiddenLineCount;

    await shell.ExpandRegionAsync(shell.Document.Rows.First(row => row.IsCollapsed));

    Assert.Equal(before - 1 + hidden, shell.Document.Rows.Count);
  }

  [Fact]
  public async Task ExpandRegion_OnANormalRow_ChangesNothing() {
    var shell = new ShellViewModel(new ComparisonSettings { CollapseUnchanged = false });
    await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "one\n", "two\n"));
    var before = shell.Document.Rows.Count;

    await shell.ExpandRegionAsync(shell.Document.Rows[0]);

    Assert.Equal(before, shell.Document.Rows.Count);
  }

  private string Write(string name, string content) {
    var path = Path.Combine(_root, name);
    File.WriteAllText(path, content);
    return path;
  }

  private static async Task WaitForIdle(ShellViewModel shell) {
    for (var i = 0; i < 200 && shell.IsBusy; i++) await Task.Delay(10);
    await Task.Delay(30);
  }
}
