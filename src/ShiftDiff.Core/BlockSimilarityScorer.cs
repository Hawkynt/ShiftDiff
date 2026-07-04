using System.Numerics;
using System.Text;

namespace ShiftDiff.Core;

public static class BlockSimilarityScorer
{
    private const int FingerprintBits = 64;
    private const int ShingleSize = 3;

    public static int TokenCount(BlockCandidate candidate, string[] oldLines, string[] newLines) =>
        TokenizeRange(oldLines, candidate.OldStart, candidate.OldEnd).Count;

    public static double ExactHashOverlap(BlockCandidate candidate, string[] oldLines, string[] newLines)
    {
        var lineCount = candidate.OldEnd - candidate.OldStart + 1;
        var matchingLines = 0;

        for (var offset = 0; offset < lineCount; offset++)
        {
            var oldHash = LineHasher.Hash(oldLines[candidate.OldStart + offset]).Raw;
            var newHash = LineHasher.Hash(newLines[candidate.NewStart + offset]).Raw;

            if (oldHash == newHash)
            {
                matchingLines++;
            }
        }

        return matchingLines / (double)lineCount;
    }

    public static double NormalizedHashOverlap(BlockCandidate candidate, string[] oldLines, string[] newLines)
    {
        var lineCount = candidate.OldEnd - candidate.OldStart + 1;
        var matchingLines = 0;

        for (var offset = 0; offset < lineCount; offset++)
        {
            var oldHash = LineHasher.Hash(oldLines[candidate.OldStart + offset]).WhitespaceNormalized;
            var newHash = LineHasher.Hash(newLines[candidate.NewStart + offset]).WhitespaceNormalized;

            if (oldHash == newHash)
            {
                matchingLines++;
            }
        }

        return matchingLines / (double)lineCount;
    }

    public static double TokenShingleSimilarity(BlockCandidate candidate, string[] oldLines, string[] newLines)
    {
        var oldShingles = BuildShingles(TokenizeRange(oldLines, candidate.OldStart, candidate.OldEnd));
        var newShingles = BuildShingles(TokenizeRange(newLines, candidate.NewStart, candidate.NewEnd));

        if (oldShingles.Count == 0 && newShingles.Count == 0)
        {
            return 1.0;
        }

        var intersectionCount = oldShingles.Intersect(newShingles).Count();
        var unionCount = oldShingles.Union(newShingles).Count();

        return intersectionCount / (double)unionCount;
    }

    public static double SimHashSimilarity(BlockCandidate candidate, string[] oldLines, string[] newLines)
    {
        var oldTokens = TokenizeRange(oldLines, candidate.OldStart, candidate.OldEnd);
        var newTokens = TokenizeRange(newLines, candidate.NewStart, candidate.NewEnd);

        if (oldTokens.Count == 0 && newTokens.Count == 0)
        {
            return 1.0;
        }

        if (oldTokens.Count == 0 || newTokens.Count == 0)
        {
            return 0.0;
        }

        var oldFingerprint = BuildFingerprint(oldTokens);
        var newFingerprint = BuildFingerprint(newTokens);
        var distance = HammingDistance(oldFingerprint, newFingerprint);

        return 1.0 - (distance / (double)FingerprintBits);
    }

    public static double BlockSizeRatio(BlockCandidate candidate, string[] oldLines, string[] newLines)
    {
        var oldLineCount = candidate.OldEnd - candidate.OldStart + 1;
        var newLineCount = candidate.NewEnd - candidate.NewStart + 1;

        var minLineCount = Math.Min(oldLineCount, newLineCount);
        var maxLineCount = Math.Max(oldLineCount, newLineCount);

        return minLineCount / (double)maxLineCount;
    }

    public static double OrderingConsistency(BlockCandidate candidate, string[] oldLines, string[] newLines)
    {
        var oldHashes = HashRange(oldLines, candidate.OldStart, candidate.OldEnd);
        var newHashes = HashRange(newLines, candidate.NewStart, candidate.NewEnd);

        var oldHashCounts = oldHashes.GroupBy(hash => hash).ToDictionary(group => group.Key, group => group.Count());
        var newHashCounts = newHashes.GroupBy(hash => hash).ToDictionary(group => group.Key, group => group.Count());
        var newOffsetByHash = newHashes
            .Select((hash, offset) => (hash, offset))
            .GroupBy(pair => pair.hash)
            .ToDictionary(group => group.Key, group => group.First().offset);

        var matchedNewOffsetsInOldOrder = new List<int>();

        foreach (var oldHash in oldHashes)
        {
            if (oldHashCounts[oldHash] == 1 && newHashCounts.TryGetValue(oldHash, out var newCount) && newCount == 1)
            {
                matchedNewOffsetsInOldOrder.Add(newOffsetByHash[oldHash]);
            }
        }

        if (matchedNewOffsetsInOldOrder.Count < 2)
        {
            return 1.0;
        }

        var concordantPairs = 0;
        var totalPairs = 0;

        for (var i = 0; i < matchedNewOffsetsInOldOrder.Count; i++)
        {
            for (var j = i + 1; j < matchedNewOffsetsInOldOrder.Count; j++)
            {
                totalPairs++;
                if (matchedNewOffsetsInOldOrder[j] > matchedNewOffsetsInOldOrder[i])
                {
                    concordantPairs++;
                }
            }
        }

        return concordantPairs / (double)totalPairs;
    }

    private static string[] HashRange(string[] lines, int start, int end)
    {
        var hashes = new string[end - start + 1];

        for (var offset = 0; offset < hashes.Length; offset++)
        {
            hashes[offset] = LineHasher.Hash(lines[start + offset]).WhitespaceNormalized;
        }

        return hashes;
    }

    public static double RarityWeightedAnchorScore(BlockCandidate candidate, string[] oldLines, string[] newLines)
    {
        var oldAnchors = AnchorDetector.Detect(oldLines);
        var newAnchors = AnchorDetector.Detect(newLines);

        var oldStrongFraction = StrongFraction(oldAnchors, candidate.OldStart, candidate.OldEnd);
        var newStrongFraction = StrongFraction(newAnchors, candidate.NewStart, candidate.NewEnd);

        return (oldStrongFraction + newStrongFraction) / 2.0;
    }

    private static double StrongFraction(LineAnchor[] anchors, int start, int end)
    {
        var count = end - start + 1;
        var strongCount = 0;

        for (var offset = 0; offset < count; offset++)
        {
            if (anchors[start + offset].Quality == AnchorQuality.Strong)
            {
                strongCount++;
            }
        }

        return strongCount / (double)count;
    }

    public static double NeighboringBlockConsistency(BlockCandidate candidate, string[] oldLines, string[] newLines)
    {
        var comparableCount = 0;
        var matchingCount = 0;

        if (candidate.OldStart > 0 && candidate.NewStart > 0)
        {
            comparableCount++;
            var oldHash = LineHasher.Hash(oldLines[candidate.OldStart - 1]).WhitespaceNormalized;
            var newHash = LineHasher.Hash(newLines[candidate.NewStart - 1]).WhitespaceNormalized;

            if (oldHash == newHash)
            {
                matchingCount++;
            }
        }

        if (candidate.OldEnd < oldLines.Length - 1 && candidate.NewEnd < newLines.Length - 1)
        {
            comparableCount++;
            var oldHash = LineHasher.Hash(oldLines[candidate.OldEnd + 1]).WhitespaceNormalized;
            var newHash = LineHasher.Hash(newLines[candidate.NewEnd + 1]).WhitespaceNormalized;

            if (oldHash == newHash)
            {
                matchingCount++;
            }
        }

        if (comparableCount == 0)
        {
            return 1.0;
        }

        return matchingCount / (double)comparableCount;
    }

    public static double CombinedScore(BlockCandidate candidate, string[] oldLines, string[] newLines) =>
        (ExactHashOverlap(candidate, oldLines, newLines)
         + NormalizedHashOverlap(candidate, oldLines, newLines)
         + TokenShingleSimilarity(candidate, oldLines, newLines)
         + SimHashSimilarity(candidate, oldLines, newLines)
         + BlockSizeRatio(candidate, oldLines, newLines)
         + OrderingConsistency(candidate, oldLines, newLines)
         + RarityWeightedAnchorScore(candidate, oldLines, newLines)
         + NeighboringBlockConsistency(candidate, oldLines, newLines)) / 8.0;

    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var builder = new StringBuilder(line.Length);

        foreach (var character in line)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
                continue;
            }

            if (builder.Length > 0)
            {
                tokens.Add(builder.ToString());
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            tokens.Add(builder.ToString());
        }

        return tokens;
    }

    private static List<string> TokenizeRange(string[] lines, int start, int end)
    {
        var tokens = new List<string>();

        for (var index = start; index <= end; index++)
        {
            tokens.AddRange(Tokenize(lines[index]));
        }

        return tokens;
    }

    private static HashSet<string> BuildShingles(List<string> tokens)
    {
        var shingles = new HashSet<string>();

        if (tokens.Count == 0)
        {
            return shingles;
        }

        if (tokens.Count < ShingleSize)
        {
            shingles.Add(string.Join(' ', tokens));
            return shingles;
        }

        for (var index = 0; index <= tokens.Count - ShingleSize; index++)
        {
            shingles.Add(string.Join(' ', tokens.Skip(index).Take(ShingleSize)));
        }

        return shingles;
    }

    private static ulong HashToken(string token)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token));

        return BitConverter.ToUInt64(hash, 0);
    }

    private static ulong BuildFingerprint(List<string> tokens)
    {
        var frequencies = tokens
            .GroupBy(token => token)
            .Select(group => new
            {
                Token = group.Key,
                Weight = group.Count(),
            });

        ulong fingerprint = 0;

        for (var bit = 0; bit < FingerprintBits; bit++)
        {
            var sum = 0;

            foreach (var frequency in frequencies)
            {
                var tokenHash = HashToken(frequency.Token);
                var bitSet = ((tokenHash >> bit) & 1) == 1;
                sum += bitSet ? frequency.Weight : -frequency.Weight;
            }

            if (sum > 0)
            {
                fingerprint |= 1UL << bit;
            }
        }

        return fingerprint;
    }

    private static int HammingDistance(ulong a, ulong b) => BitOperations.PopCount(a ^ b);
}
