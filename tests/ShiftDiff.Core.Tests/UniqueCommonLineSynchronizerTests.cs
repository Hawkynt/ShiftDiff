using Xunit;

namespace ShiftDiff.Core.Tests;

public class UniqueCommonLineSynchronizerTests {
  [Fact]
  public void FindAnchors_NoCommonLines_ReturnsEmpty() {
    var oldKeys = new[] { "a", "b", "c" };
    var newKeys = new[] { "x", "y", "z" };

    var anchors = UniqueCommonLineSynchronizer.FindAnchors(oldKeys, newKeys, StringComparison.Ordinal);

    Assert.Empty(anchors);
  }

  [Fact]
  public void FindAnchors_AllLinesUniqueAndShared_ReturnsEveryLineInOrder() {
    var oldKeys = new[] { "a", "b", "c" };
    var newKeys = new[] { "a", "b", "c" };

    var anchors = UniqueCommonLineSynchronizer.FindAnchors(oldKeys, newKeys, StringComparison.Ordinal);

    Assert.Equal(new[] { (0, 0), (1, 1), (2, 2) }, anchors);
  }

  [Fact]
  public void FindAnchors_DuplicatedLine_IsExcludedFromAnchors() {
    // "b" appears twice in oldKeys, so it cannot anchor even though it also appears in newKeys.
    var oldKeys = new[] { "a", "b", "b", "c" };
    var newKeys = new[] { "a", "b", "c" };

    var anchors = UniqueCommonLineSynchronizer.FindAnchors(oldKeys, newKeys, StringComparison.Ordinal);

    Assert.Equal(new[] { (0, 0), (3, 2) }, anchors);
  }

  [Fact]
  public void FindAnchors_OutOfOrderMatches_KeepsOnlyLongestNonCrossingSubsequence() {
    // "y" (old 1, new 2) and "z" (old 2, new 1) cross — only a non-crossing subset can anchor.
    var oldKeys = new[] { "x", "y", "z" };
    var newKeys = new[] { "x", "z", "y" };

    var anchors = UniqueCommonLineSynchronizer.FindAnchors(oldKeys, newKeys, StringComparison.Ordinal);

    Assert.Equal(2, anchors.Length);
    Assert.Equal((0, 0), anchors[0]);
    for (var i = 1; i < anchors.Length; i++) {
      Assert.True(anchors[i].NewIndex > anchors[i - 1].NewIndex);
      Assert.True(anchors[i].OldIndex > anchors[i - 1].OldIndex);
    }
  }

  [Fact]
  public void FindAnchors_CaseInsensitiveComparison_MatchesRegardlessOfCase() {
    var oldKeys = new[] { "Hello" };
    var newKeys = new[] { "hello" };

    var anchors = UniqueCommonLineSynchronizer.FindAnchors(oldKeys, newKeys, StringComparison.OrdinalIgnoreCase);

    Assert.Equal(new[] { (0, 0) }, anchors);
  }

  [Fact]
  public void FindAnchors_EmptyInputs_ReturnsEmpty() {
    var anchors = UniqueCommonLineSynchronizer.FindAnchors([], [], StringComparison.Ordinal);

    Assert.Empty(anchors);
  }
}
