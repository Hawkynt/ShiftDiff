using System.Linq;

namespace ShiftDiff.Core;

public static class AnchorDetector
{
    public static int DuplicateCount(string[] lines, int index)
    {
        var targetHash = LineHasher.Hash(lines[index]).WhitespaceNormalized;
        return lines.Count(line => LineHasher.Hash(line).WhitespaceNormalized == targetHash);
    }

    /// <summary>
    /// Batch form of <see cref="DuplicateCount"/> — computes every line's duplicate count in one
    /// O(n) pass instead of the O(n) rescan per call that made repeated per-line lookups (e.g.
    /// <see cref="BlockClassifier.Classify"/> over a candidate's whole span) effectively O(n^2).
    /// </summary>
    public static int[] DuplicateCounts(string[] lines)
    {
        var hashes = lines.Select(line => LineHasher.Hash(line).WhitespaceNormalized).ToArray();
        var counts = hashes.GroupBy(hash => hash).ToDictionary(group => group.Key, group => group.Count());

        return hashes.Select(hash => counts[hash]).ToArray();
    }

    public static LineAnchor[] Detect(string[] lines)
    {
        var whitespaceNormalizedCounts = lines
            .Select(line => LineHasher.Hash(line).WhitespaceNormalized)
            .GroupBy(hash => hash)
            .ToDictionary(group => group.Key, group => group.Count());

        var anchors = new LineAnchor[lines.Length];

        for (var index = 0; index < lines.Length; index++)
        {
            anchors[index] = new LineAnchor(index, lines[index], ClassifyLine(lines[index], whitespaceNormalizedCounts));
        }

        return anchors;
    }

    private static AnchorQuality ClassifyLine(string line, Dictionary<string, int> whitespaceNormalizedCounts)
    {
        var trimmed = line.Trim();

        if (trimmed is "" or "{" or "}" or "else")
        {
            return AnchorQuality.Rejected;
        }

        if (whitespaceNormalizedCounts[LineHasher.Hash(line).WhitespaceNormalized] > 1 || trimmed.Length < 8)
        {
            return AnchorQuality.Weak;
        }

        return AnchorQuality.Strong;
    }
}
