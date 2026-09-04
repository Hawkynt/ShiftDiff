using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class LineTokenizerTests {
  [Fact]
  public void Tokenize_SplitsWordAndNonWordRuns() {
    var tokens = LineTokenizer.Tokenize("foo, bar!");
    Assert.Equal(new[] { "foo", ", ", "bar", "!" }, tokens);
  }

  [Theory]
  [InlineData("")]
  [InlineData("foo")]
  [InlineData("foo_bar123")]
  [InlineData("foo, bar!")]
  [InlineData("  leading and trailing  ")]
  [InlineData("a\tb\nc")]
  [InlineData("!!!")]
  public void Tokenize_ConcatenationReconstructsTheOriginalLine(string line) {
    var tokens = LineTokenizer.Tokenize(line);
    Assert.Equal(line, string.Concat(tokens));
  }

  [Fact]
  public void TokenizeSourceCode_TreatsDoubleQuotedStringAsSingleToken() {
    var tokens = LineTokenizer.TokenizeSourceCode("var x = \"hello world\";");
    Assert.Contains("\"hello world\"", tokens);
  }

  [Fact]
  public void TokenizeSourceCode_TreatsSingleQuotedCharAsSingleToken() {
    var tokens = LineTokenizer.TokenizeSourceCode("char c = 'a';");
    Assert.Contains("'a'", tokens);
  }

  [Fact]
  public void TokenizeSourceCode_KeepsEscapedQuoteInsideLiteral() {
    var tokens = LineTokenizer.TokenizeSourceCode("\"a\\\"b\"");
    Assert.Equal(new[] { "\"a\\\"b\"" }, tokens);
  }

  [Fact]
  public void TokenizeSourceCode_UnterminatedLiteralConsumesRestOfLine() {
    var tokens = LineTokenizer.TokenizeSourceCode("\"abc");
    Assert.Equal(new[] { "\"abc" }, tokens);
  }

  [Theory]
  [InlineData("")]
  [InlineData("var x = \"hello world\";")]
  [InlineData("char c = 'a';")]
  [InlineData("\"a\\\"b\"")]
  [InlineData("\"abc")]
  [InlineData("no literals here")]
  public void TokenizeSourceCode_ConcatenationReconstructsTheOriginalLine(string line) {
    var tokens = LineTokenizer.TokenizeSourceCode(line);
    Assert.Equal(line, string.Concat(tokens));
  }

  [Fact]
  public void TokenizeSourceCode_TreatsLineCommentAsSingleToken() {
    var tokens = LineTokenizer.TokenizeSourceCode("x = 1; // note");
    Assert.Equal("// note", tokens[^1]);
  }

  [Fact]
  public void TokenizeSourceCode_SlashInsideStringLiteral_NotTreatedAsComment() {
    var tokens = LineTokenizer.TokenizeSourceCode("\"a//b\"");
    Assert.Equal(new[] { "\"a//b\"" }, tokens);
  }

  [Fact]
  public void TokenizeSourceCode_SingleSlash_NotTreatedAsCommentStart() {
    var tokens = LineTokenizer.TokenizeSourceCode("a / b");
    Assert.Equal(new[] { "a", " / ", "b" }, tokens);
  }
}
