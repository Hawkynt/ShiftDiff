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

    public static LineAnchor[] Detect(string[] lines) => DetectWithDuplicateCounts(lines).Anchors;

    /// <summary>
    /// Combined form of <see cref="Detect"/> and <see cref="DuplicateCounts"/> — callers that need
    /// both (e.g. <see cref="BlockClassifier.Classify"/>) previously hashed every line twice, once
    /// per method. This computes the hash pass exactly once and derives both results from it.
    /// </summary>
    public static (LineAnchor[] Anchors, int[] DuplicateCounts) DetectWithDuplicateCounts(string[] lines)
    {
        var lineHashes = lines.Select(line => LineHasher.Hash(line).WhitespaceNormalized).ToArray();
        var whitespaceNormalizedCounts = lineHashes
            .GroupBy(hash => hash)
            .ToDictionary(group => group.Key, group => group.Count());

        var anchors = new LineAnchor[lines.Length];
        var duplicateCounts = new int[lines.Length];

        for (var index = 0; index < lines.Length; index++)
        {
            anchors[index] = new LineAnchor(index, lines[index], ClassifyLine(lines[index], lineHashes[index], whitespaceNormalizedCounts));
            duplicateCounts[index] = whitespaceNormalizedCounts[lineHashes[index]];
        }

        return (anchors, duplicateCounts);
    }

    private static AnchorQuality ClassifyLine(string line, string whitespaceNormalizedHash, Dictionary<string, int> whitespaceNormalizedCounts)
    {
        var trimmed = line.Trim();

        if (trimmed is "" or "{" or "}" or "else")
        {
            return AnchorQuality.Rejected;
        }

        if (whitespaceNormalizedCounts[whitespaceNormalizedHash] > 1 || trimmed.Length < 8)
        {
            return AnchorQuality.Weak;
        }

        return AnchorQuality.Strong;
    }
}
