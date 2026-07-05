using System.Security.Cryptography;
using System.Text;

namespace ShiftDiff.Core;

public sealed record LineHash(string Raw, string Trimmed, string WhitespaceNormalized, string TokenNormalized);

public static class LineHasher
{
    public static LineHash Hash(string line)
    {
        var trimmed = LineNormalizer.Trim(line);

        return new LineHash(
            ComputeHash(line),
            ComputeHash(trimmed),
            ComputeHash(LineNormalizer.NormalizeWhitespace(trimmed)),
            ComputeHash(LineNormalizer.RemoveWhitespace(line)));
    }

    /// <summary>
    /// Single-tier form of <see cref="Hash"/> — most callers (<see cref="AnchorDetector"/>,
    /// <see cref="BlockBuilder"/>, most of <see cref="BlockSimilarityScorer"/>) only ever read one
    /// tier of the 4 <c>Hash</c> computes, so this avoids the other 3 SHA-256 passes per line.
    /// </summary>
    public static string HashRaw(string line) => ComputeHash(line);

    /// <summary>See <see cref="HashRaw"/> — whitespace-normalized tier only.</summary>
    public static string HashWhitespaceNormalized(string line) =>
        ComputeHash(LineNormalizer.NormalizeWhitespace(LineNormalizer.Trim(line)));

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
