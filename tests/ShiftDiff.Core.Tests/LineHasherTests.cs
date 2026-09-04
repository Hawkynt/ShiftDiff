using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class LineHasherTests {
  [Fact]
  public void IdenticalLines_ProduceIdenticalRawHash() {
    var a = LineHasher.Hash("foo bar");
    var b = LineHasher.Hash("foo bar");
    Assert.Equal(a.Raw, b.Raw);
  }

  [Fact]
  public void DifferentLines_ProduceDifferentRawHash() {
    var a = LineHasher.Hash("foo bar");
    var b = LineHasher.Hash("foo baz");
    Assert.NotEqual(a.Raw, b.Raw);
  }

  [Fact]
  public void LeadingAndTrailingWhitespace_SharesTrimmedAndWhitespaceNormalizedButNotRaw() {
    var padded = LineHasher.Hash("  foo  ");
    var bare = LineHasher.Hash("foo");
    Assert.Equal(padded.Trimmed, bare.Trimmed);
    Assert.Equal(padded.WhitespaceNormalized, bare.WhitespaceNormalized);
    Assert.NotEqual(padded.Raw, bare.Raw);
  }

  [Fact]
  public void InternalWhitespaceRuns_ShareWhitespaceNormalizedButNotTrimmed() {
    var runs = LineHasher.Hash("foo   bar");
    var single = LineHasher.Hash("foo bar");
    Assert.Equal(runs.WhitespaceNormalized, single.WhitespaceNormalized);
    Assert.NotEqual(runs.Trimmed, single.Trimmed);
  }

  [Fact]
  public void RemovingAllWhitespace_SharesTokenNormalizedButNothingElse() {
    var spaced = LineHasher.Hash("foo bar");
    var joined = LineHasher.Hash("foobar");
    Assert.Equal(spaced.TokenNormalized, joined.TokenNormalized);
    Assert.NotEqual(spaced.Raw, joined.Raw);
    Assert.NotEqual(spaced.Trimmed, joined.Trimmed);
  }

  [Theory]
  [InlineData("foo bar")]
  [InlineData("  foo  ")]
  [InlineData("foo   bar")]
  [InlineData("")]
  public void HashRaw_MatchesRawTierOfFullHash(string line) {
    Assert.Equal(LineHasher.Hash(line).Raw, LineHasher.HashRaw(line));
  }

  [Theory]
  [InlineData("foo bar")]
  [InlineData("  foo  ")]
  [InlineData("foo   bar")]
  [InlineData("")]
  public void HashWhitespaceNormalized_MatchesWhitespaceNormalizedTierOfFullHash(string line) {
    Assert.Equal(LineHasher.Hash(line).WhitespaceNormalized, LineHasher.HashWhitespaceNormalized(line));
  }
}
