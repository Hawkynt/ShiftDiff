using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class LineTokenizerTests
{
    [Fact]
    public void Tokenize_SplitsWordAndNonWordRuns()
    {
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
    public void Tokenize_ConcatenationReconstructsTheOriginalLine(string line)
    {
        var tokens = LineTokenizer.Tokenize(line);
        Assert.Equal(line, string.Concat(tokens));
    }
}
