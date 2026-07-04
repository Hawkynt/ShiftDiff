namespace ShiftDiff.Core;

public static class TokenDiffer
{
    public static TokenChange[] Diff(string oldLine, string newLine, bool ignoreCase = false, WhitespaceMode whitespaceMode = WhitespaceMode.None)
    {
        var oldTokens = LineTokenizer.Tokenize(oldLine);
        var newTokens = LineTokenizer.Tokenize(newLine);
        return LineDiffer.Diff(oldTokens, newTokens, ignoreCase, whitespaceMode)
            .Select(c => new TokenChange(c.ChangeType, c.OldLine, c.NewLine))
            .ToArray();
    }
}
