namespace ShiftDiff.Core;

public static class ConfidenceClassifier
{
    // FR-015 starting values — spec calls these out as placeholders requiring empirical tuning.
    private const double CertainThreshold = 0.8;
    private const double LikelyThreshold = 0.65;
    private const double PossibleThreshold = 0.5;
    private const double WeakThreshold = 0.3;

    public static Confidence Classify(double score)
    {
        if (score >= CertainThreshold) return Confidence.Certain;
        if (score >= LikelyThreshold) return Confidence.Likely;
        if (score >= PossibleThreshold) return Confidence.Possible;
        if (score >= WeakThreshold) return Confidence.Weak;
        return Confidence.Rejected;
    }
}
