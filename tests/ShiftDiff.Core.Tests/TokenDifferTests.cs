using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class TokenDifferTests {
  [Fact]
  public void Diff_classifies_identical_lines_as_all_unchanged() {
    var changes = TokenDiffer.Diff("the quick fox", "the quick fox");
    Assert.All(changes, c => Assert.Equal(ChangeType.Unchanged, c.ChangeType));
  }

  [Fact]
  public void Diff_isolates_a_single_changed_word_leaving_surrounding_tokens_unchanged() {
    var changes = TokenDiffer.Diff("the quick fox", "the slow fox");

    var unchangedTokens = changes
        .Where(c => c.ChangeType == ChangeType.Unchanged)
        .Select(c => c.OldToken)
        .ToArray();
    Assert.Contains("the", unchangedTokens);
    Assert.Contains(" ", unchangedTokens);
    Assert.Contains("fox", unchangedTokens);

    var changedTokens = changes.Where(c => c.ChangeType != ChangeType.Unchanged).ToArray();
    Assert.NotEmpty(changedTokens);
    Assert.All(changedTokens, c =>
        Assert.True((c.OldToken ?? c.NewToken) == "quick" || (c.OldToken ?? c.NewToken) == "slow"));
  }

  [Fact]
  public void Diff_treats_whitespace_only_change_between_tokens_as_a_change() {
    var changes = TokenDiffer.Diff("a  b", "a b");

    var unchangedTokens = changes
        .Where(c => c.ChangeType == ChangeType.Unchanged)
        .Select(c => c.OldToken)
        .ToArray();
    Assert.DoesNotContain("  ", unchangedTokens);

    Assert.Contains(changes, c => c.ChangeType != ChangeType.Unchanged);
  }

  [Fact]
  public void Diff_WithIgnoreCase_TreatsDifferentlyCasedTokensAsUnchanged() {
    var changes = TokenDiffer.Diff("The Fox", "the fox", ignoreCase: true);

    Assert.All(changes, c => Assert.Equal(ChangeType.Unchanged, c.ChangeType));
  }

  [Fact]
  public void Diff_WithWhitespaceModeRemoveAll_TreatsDifferentWhitespaceRunsAsUnchanged() {
    var changes = TokenDiffer.Diff("a  b", "a b", whitespaceMode: WhitespaceMode.RemoveAll);

    Assert.All(changes, c => Assert.Equal(ChangeType.Unchanged, c.ChangeType));
  }

  [Fact]
  public void Diff_DefaultsToGenericTokenization_QuotedStringSplitsAtWordBoundaries() {
    var changes = TokenDiffer.Diff("x = \"foo\";", "x = \"foo\";");

    Assert.Contains(changes, c => c.OldToken == "foo");
  }

  [Fact]
  public void Diff_WithIsSourceCode_TreatsQuotedStringAsOneToken() {
    var changes = TokenDiffer.Diff("x = \"foo\";", "x = \"bar\";", isSourceCode: true);

    var changed = changes.Where(c => c.ChangeType != ChangeType.Unchanged).ToArray();
    Assert.Contains(changed, c => c.OldToken == "\"foo\"");
    Assert.Contains(changed, c => c.NewToken == "\"bar\"");
  }
}
