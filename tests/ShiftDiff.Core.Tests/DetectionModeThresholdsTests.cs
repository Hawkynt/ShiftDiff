using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class DetectionModeThresholdsTests
{
    [Fact]
    public void MovedConfidenceThreshold_returns_strict_threshold()
    {
        Assert.Equal(0.9, DetectionModeThresholds.MovedConfidenceThreshold(DetectionMode.Strict));
    }

    [Fact]
    public void MovedConfidenceThreshold_returns_balanced_threshold()
    {
        Assert.Equal(0.5, DetectionModeThresholds.MovedConfidenceThreshold(DetectionMode.Balanced));
    }

    [Fact]
    public void MovedConfidenceThreshold_returns_aggressive_threshold()
    {
        Assert.Equal(0.35, DetectionModeThresholds.MovedConfidenceThreshold(DetectionMode.Aggressive));
    }
}
