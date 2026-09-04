namespace ShiftDiff.Core;

public static class DetectionModeThresholds {
  // FR-016 placeholder bands pending empirical tuning, same disclaimer as ConfidenceClassifier.
  // Balanced preserves BlockClassifier's pre-FR-016 hardcoded 0.5 default exactly.
  public static double MovedConfidenceThreshold(DetectionMode mode) => mode switch {
    DetectionMode.Strict => 0.9,
    DetectionMode.Balanced => 0.5,
    DetectionMode.Aggressive => 0.35,
    _ => 0.5
  };
}
