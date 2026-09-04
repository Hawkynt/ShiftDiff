using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class AnchorDetectorTests {
  [Fact]
  public void Detect_rejects_a_blank_line() {
    var result = AnchorDetector.Detect(new[] { "" });

    Assert.Equal(AnchorQuality.Rejected, result[0].Quality);
  }

  [Fact]
  public void Detect_rejects_brace_and_else_boilerplate_even_with_surrounding_whitespace() {
    var result = AnchorDetector.Detect(new[] { "  {  ", "  }  ", "  else  " });

    Assert.All(result, r => Assert.Equal(AnchorQuality.Rejected, r.Quality));
  }

  [Fact]
  public void Detect_marks_a_duplicated_line_as_weak_for_both_occurrences() {
    var result = AnchorDetector.Detect(new[] { "public static LineHash Hash(string line)", "public static LineHash Hash(string line)" });

    Assert.Equal(AnchorQuality.Weak, result[0].Quality);
    Assert.Equal(AnchorQuality.Weak, result[1].Quality);
  }

  [Fact]
  public void Detect_marks_a_short_unique_line_as_weak() {
    var result = AnchorDetector.Detect(new[] { "x = 1;" });

    Assert.Equal(AnchorQuality.Weak, result[0].Quality);
  }

  [Fact]
  public void Detect_marks_a_long_unique_line_as_strong() {
    var result = AnchorDetector.Detect(new[] { "public static LineHash Hash(string line)" });

    Assert.Equal(AnchorQuality.Strong, result[0].Quality);
  }

  [Fact]
  public void Detect_preserves_input_index_in_returned_anchors() {
    var result = AnchorDetector.Detect(new[] { "public static LineHash Hash(string line)", "{", "x = 1;" });

    Assert.Equal(0, result[0].Index);
    Assert.Equal(1, result[1].Index);
    Assert.Equal(2, result[2].Index);
  }

  [Fact]
  public void DuplicateCount_returns_one_for_a_unique_line() {
    var lines = new[] { "public static LineHash Hash(string line)", "x = 1;" };

    Assert.Equal(1, AnchorDetector.DuplicateCount(lines, 0));
  }

  [Fact]
  public void DuplicateCount_counts_whitespace_normalized_duplicates_across_the_whole_array() {
    var lines = new[]
    {
            "public static LineHash Hash(string line)",
            "x = 1;",
            "public   static   LineHash   Hash(string   line)",
        };

    Assert.Equal(2, AnchorDetector.DuplicateCount(lines, 0));
    Assert.Equal(2, AnchorDetector.DuplicateCount(lines, 2));
    Assert.Equal(1, AnchorDetector.DuplicateCount(lines, 1));
  }

  [Fact]
  public void DuplicateCounts_matches_DuplicateCount_for_every_index() {
    var lines = new[]
    {
            "public static LineHash Hash(string line)",
            "x = 1;",
            "public   static   LineHash   Hash(string   line)",
            "y = 2;",
        };

    var counts = AnchorDetector.DuplicateCounts(lines);

    for (var index = 0; index < lines.Length; index++) {
      Assert.Equal(AnchorDetector.DuplicateCount(lines, index), counts[index]);
    }
  }

  [Fact]
  public void DuplicateCounts_returns_one_entry_per_input_line() {
    var lines = new[] { "a", "b", "c" };

    Assert.Equal(3, AnchorDetector.DuplicateCounts(lines).Length);
  }

  [Fact]
  public void DetectWithDuplicateCounts_ReturnsSameAnchorsAsDetectAndSameCountsAsDuplicateCounts() {
    var lines = new[]
    {
            "public static LineHash Hash(string line)",
            "x = 1;",
            "public   static   LineHash   Hash(string   line)",
            "y = 2;",
        };

    var (anchors, counts) = AnchorDetector.DetectWithDuplicateCounts(lines);

    Assert.Equal(AnchorDetector.Detect(lines), anchors);
    Assert.Equal(AnchorDetector.DuplicateCounts(lines), counts);
  }

  [Fact]
  public void Detect_PreCancelledToken_ThrowsOperationCanceledException() {
    var lines = new[] { "public static LineHash Hash(string line)", "x = 1;" };
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    Assert.Throws<OperationCanceledException>(() => AnchorDetector.Detect(lines, cts.Token));
  }

  [Fact]
  public void DetectWithDuplicateCounts_PreCancelledToken_ThrowsOperationCanceledException() {
    var lines = new[] { "public static LineHash Hash(string line)", "x = 1;" };
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    Assert.Throws<OperationCanceledException>(() => AnchorDetector.DetectWithDuplicateCounts(lines, cts.Token));
  }
}
