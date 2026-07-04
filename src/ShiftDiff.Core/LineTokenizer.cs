namespace ShiftDiff.Core;

public static class LineTokenizer
{
    public static string[] Tokenize(string line)
    {
        if (line.Length == 0)
        {
            return [];
        }

        var tokens = new List<string>();
        var start = 0;
        var inWord = IsWordChar(line[0]);

        for (var i = 1; i < line.Length; i++)
        {
            var charIsWord = IsWordChar(line[i]);
            if (charIsWord != inWord)
            {
                tokens.Add(line[start..i]);
                start = i;
                inWord = charIsWord;
            }
        }

        tokens.Add(line[start..]);
        return tokens.ToArray();
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
