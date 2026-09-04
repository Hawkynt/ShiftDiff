using System.Diagnostics;
using System.Text;
using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

// FR-050/AC-009: a large file has to stay usable. These bounds are deliberately
// loose — they catch an accidental quadratic, not a few milliseconds of drift.
public class LargeDocumentTests {
  [Fact]
  public void BuildTwoWay_HundredThousandLines_CompletesWithinTheBudget() {
    var oldText = Generate(100_000);
    var newText = oldText.Replace("line 50000", "LINE FIFTY THOUSAND");
    var settings = new ComparisonSettings { CollapseUnchanged = true };

    var stopwatch = Stopwatch.StartNew();
    var result = FileComparer.CompareSourceFiles(
        Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText), "old.txt", "new.txt");
    var document = DiffDocumentBuilder.BuildTwoWay(result, settings);
    stopwatch.Stop();

    Assert.True(document.Summary.HasDifferences);
    Assert.True(
        stopwatch.Elapsed < TimeSpan.FromSeconds(20),
        $"comparing and building 100k lines took {stopwatch.Elapsed}");
  }

  [Fact]
  public void BuildTwoWay_WithFolding_KeepsTheRowCountSmallForALargeFile() {
    var oldText = Generate(50_000);
    var newText = oldText.Replace("line 25000", "CHANGED");

    var result = FileComparer.CompareSourceFiles(
        Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText), "old.txt", "new.txt");
    var document = DiffDocumentBuilder.BuildTwoWay(result, new ComparisonSettings { CollapseUnchanged = true });

    // One change plus its context and two folded regions — not 50,000 rows.
    Assert.True(document.Rows.Count < 50, $"{document.Rows.Count} rows survived folding");
  }

  [Fact]
  public void Overview_ForALargeDocument_StaysProportionalToTheChangeCount() {
    var oldText = Generate(20_000);
    // Anchored on the whole line so "line 100" does not also match "line 1000".
    var newText = oldText.Replace("line 100 of", "A of").Replace("line 10000 of", "B of");

    var result = FileComparer.CompareSourceFiles(
        Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText), "old.txt", "new.txt");
    var document = DiffDocumentBuilder.BuildTwoWay(result, new ComparisonSettings { CollapseUnchanged = false });

    Assert.Equal(2, document.Overview.Count);
    Assert.All(document.Overview, stripe => Assert.InRange(stripe.Start, 0, 1));
  }

  [Fact]
  public async Task Shell_CancellingALargeComparison_LeavesTheSessionUsable() {
    var text = Generate(60_000);
    var shell = new ShellViewModel();

    var comparison = shell.OpenAsync(InMemoryComparisonSource.FromText("big.txt", text, text.Replace("line 5", "X")));
    shell.CancelAnalysis();
    await comparison;

    // Either the analysis finished or it was abandoned; neither may throw.
    Assert.NotNull(shell.Document);
  }

  private static string Generate(int lines) =>
      string.Join("\n", Enumerable.Range(0, lines).Select(i => $"line {i} of the generated file"));
}
