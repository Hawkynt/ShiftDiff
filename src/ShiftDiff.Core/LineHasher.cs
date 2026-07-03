using System.Security.Cryptography;
using System.Text;

namespace ShiftDiff.Core;

public sealed record LineHash(string Raw, string Trimmed, string WhitespaceNormalized, string TokenNormalized);

public static class LineHasher
{
    public static LineHash Hash(string line)
    {
        var trimmed = line.Trim();

        return new LineHash(
            ComputeHash(line),
            ComputeHash(trimmed),
            ComputeHash(NormalizeWhitespace(trimmed)),
            ComputeHash(RemoveWhitespace(line)));
    }

    private static string NormalizeWhitespace(string line)
    {
        var builder = new StringBuilder(line.Length);
        var previousWasWhitespace = false;

        foreach (var character in line)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }

    private static string RemoveWhitespace(string line)
    {
        var builder = new StringBuilder(line.Length);

        foreach (var character in line)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
