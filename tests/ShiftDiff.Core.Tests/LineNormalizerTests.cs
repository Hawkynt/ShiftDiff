using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class LineNormalizerTests
{
    [Fact]
    public void Trim_RemovesLeadingAndTrailingWhitespaceOnly()
    {
        Assert.Equal("a  b", LineNormalizer.Trim("  a  b  "));
    }

    [Fact]
    public void NormalizeWhitespace_CollapsesInternalRunsToASingleSpace()
    {
        Assert.Equal("a b c", LineNormalizer.NormalizeWhitespace("a   b\tc"));
    }

    [Fact]
    public void NormalizeWhitespace_DoesNotTrimLeadingOrTrailingWhitespace()
    {
        Assert.Equal(" a b ", LineNormalizer.NormalizeWhitespace("  a   b  "));
    }

    [Fact]
    public void RemoveWhitespace_StripsAllWhitespaceIncludingInternal()
    {
        Assert.Equal("abc", LineNormalizer.RemoveWhitespace(" a b c "));
    }
}
