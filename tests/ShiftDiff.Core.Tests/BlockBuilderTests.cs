using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class BlockBuilderTests {
  [Fact]
  public void Build_returns_no_candidates_for_identical_files() {
    var lines = new[]
    {
            "alpha line number one is long enough",
            "beta line number two is long enough",
            "gamma line number three is long enough",
        };

    var result = BlockBuilder.Build(lines, lines);

    Assert.Empty(result);
  }

  [Fact]
  public void Build_finds_a_single_long_unique_line_moved_to_a_new_position() {
    var oldLines = new[]
    {
            "unique long line number one for moving",
            "some other original line content aaaa",
            "some other original line content bbbb",
        };
    var newLines = new[]
    {
            "totally different new content xxxxxxx",
            "totally different new content yyyyyyy",
            "totally different new content zzzzzzz",
            "unique long line number one for moving",
        };

    var result = BlockBuilder.Build(oldLines, newLines);

    var candidate = Assert.Single(result);
    Assert.Equal(new BlockCandidate(0, 0, 3, 3), candidate);
  }

  [Fact]
  public void Build_groups_a_contiguous_moved_block_into_one_candidate_spanning_the_whole_block() {
    var oldLines = new[]
    {
            "filler original line zero content aaa",
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };
    var newLines = new[]
    {
            "filler new line zero content bbb",
            "filler new line one content ccc",
            "filler new line two content ddd",
            "filler new line three content eee",
            "filler new line four content fff",
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };

    var result = BlockBuilder.Build(oldLines, newLines);

    var candidate = Assert.Single(result);
    Assert.Equal(new BlockCandidate(1, 3, 5, 7), candidate);
  }

  [Fact]
  public void Build_keeps_two_non_adjacent_moved_lines_as_separate_candidates() {
    var oldLines = new[]
    {
            "first moved unique line content here aa",
            "middle filler line stays only in old bb",
            "second moved unique line content here cc",
        };
    var newLines = new[]
    {
            "totally unrelated filler for new dddddd",
            "first moved unique line content here aa",
            "another new filler line only in new eee",
            "second moved unique line content here cc",
        };

    var result = BlockBuilder.Build(oldLines, newLines);

    Assert.Equal(2, result.Length);
    Assert.Contains(new BlockCandidate(0, 0, 1, 1), result);
    Assert.Contains(new BlockCandidate(2, 2, 3, 3), result);
  }

  [Fact]
  public void Build_ignores_a_moved_line_that_is_too_short_to_be_a_strong_anchor() {
    var oldLines = new[] { "x = 1;" };
    var newLines = new[] { "y = 2;", "x = 1;" };

    var result = BlockBuilder.Build(oldLines, newLines);

    Assert.Empty(result);
  }

  [Fact]
  public void Build_PreCancelledToken_ThrowsOperationCanceledException() {
    var oldLines = new[] { "public static LineHash Hash(string line)", "x = 1;" };
    var newLines = new[] { "x = 1;", "public static LineHash Hash(string line)" };
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    Assert.Throws<OperationCanceledException>(() => BlockBuilder.Build(oldLines, newLines, cancellationToken: cts.Token));
  }
}
