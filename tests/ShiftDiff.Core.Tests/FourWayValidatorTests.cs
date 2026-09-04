using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class FourWayValidatorTests {
  [Fact]
  public void Validate_CandidateMatchesExpected_ReturnsMatchesTrueNoDiscrepancies() {
    var expected = new[] { "a", "b", "c" };
    var candidate = new[] { "a", "b", "c" };

    var result = FourWayValidator.Validate(expected, candidate);

    Assert.True(result.Matches);
    Assert.Empty(result.Discrepancies);
  }

  [Fact]
  public void Validate_CandidateMissesResolvedLine_ReturnsMatchesFalseWithDiscrepancy() {
    var expected = new[] { "a", "B-local", "c" };
    var candidate = new[] { "a", "B-remote", "c" };

    var result = FourWayValidator.Validate(expected, candidate);

    Assert.False(result.Matches);
    var discrepancy = Assert.Single(result.Discrepancies);
    Assert.Equal(ChangeType.Edited, discrepancy.ChangeType);
  }

  [Fact]
  public void Validate_CandidateHasExtraLine_ReturnsMatchesFalse() {
    var expected = new[] { "a", "b", "c" };
    var candidate = new[] { "a", "b", "extra", "c" };

    var result = FourWayValidator.Validate(expected, candidate);

    Assert.False(result.Matches);
    Assert.NotEmpty(result.Discrepancies);
  }

  [Fact]
  public void Validate_EndToEnd_ResolvedConflictCorrectlyIncorporated_Matches() {
    var baseLines = new[] { "a", "b", "c" };
    var localLines = new[] { "a", "B-local", "c" };
    var remoteLines = new[] { "a", "B-remote", "c" };
    var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);
    var resolutions = new Dictionary<int, ConflictResolutionChoice> {
      [1] = new ConflictResolutionChoice(ConflictResolution.UseLocal),
    };
    var expected = ThreeWayMerger.MergeWithResolutions(changes, resolutions).Lines;

    var candidate = new[] { "a", "B-local", "c" };
    var result = FourWayValidator.Validate(expected, candidate);

    Assert.True(result.Matches);
  }

  [Fact]
  public void Validate_EndToEnd_CandidateIgnoresSelectedResolution_DoesNotMatch() {
    var baseLines = new[] { "a", "b", "c" };
    var localLines = new[] { "a", "B-local", "c" };
    var remoteLines = new[] { "a", "B-remote", "c" };
    var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);
    var resolutions = new Dictionary<int, ConflictResolutionChoice> {
      [1] = new ConflictResolutionChoice(ConflictResolution.UseLocal),
    };
    var expected = ThreeWayMerger.MergeWithResolutions(changes, resolutions).Lines;

    var candidate = new[] { "a", "B-remote", "c" };
    var result = FourWayValidator.Validate(expected, candidate);

    Assert.False(result.Matches);
  }
}
