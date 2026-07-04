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

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
