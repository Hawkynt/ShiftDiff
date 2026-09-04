using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class ConfidenceClassifierTests {
  [Theory]
  [InlineData(1.0, Confidence.Certain)]
  [InlineData(0.8, Confidence.Certain)]
  [InlineData(0.79, Confidence.Likely)]
  [InlineData(0.65, Confidence.Likely)]
  [InlineData(0.64, Confidence.Possible)]
  [InlineData(0.5, Confidence.Possible)]
  [InlineData(0.49, Confidence.Weak)]
  [InlineData(0.3, Confidence.Weak)]
  [InlineData(0.29, Confidence.Rejected)]
  [InlineData(0.0, Confidence.Rejected)]
  public void Classify_bands_a_score_into_the_expected_confidence_level(double score, Confidence expected) {
    Assert.Equal(expected, ConfidenceClassifier.Classify(score));
  }

  [Fact]
  public void Classify_bands_the_moved_candidate_golden_score_as_certain() {
    Assert.Equal(Confidence.Certain, ConfidenceClassifier.Classify(0.875));
  }

  [Fact]
  public void Classify_bands_the_uncertain_candidate_golden_score_as_weak() {
    Assert.Equal(Confidence.Weak, ConfidenceClassifier.Classify(0.4036458333333333));
  }
}
