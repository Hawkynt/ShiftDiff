using System.Text;

namespace ShiftDiff.Core;

public static class BlockSimilarityScorer
{
    private const int ShingleSize = 3;

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
}
