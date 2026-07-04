namespace ShiftDiff.Core;

public static class AnchorDetector
{
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
