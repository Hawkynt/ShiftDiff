namespace ShiftDiff.Core;

public static class TokenDiffer
{
    public static TokenChange[] Diff(string oldLine, string newLine, bool ignoreCase = false, WhitespaceMode whitespaceMode = WhitespaceMode.None, bool isSourceCode = false)
    {
        var oldTokens = isSourceCode ? LineTokenizer.TokenizeSourceCode(oldLine) : LineTokenizer.Tokenize(oldLine);
        var newTokens = isSourceCode ? LineTokenizer.TokenizeSourceCode(newLine) : LineTokenizer.Tokenize(newLine);
        return LineDiffer.Diff(oldTokens, newTokens, ignoreCase, whitespaceMode)
            .Select(c => new TokenChange(c.ChangeType, c.OldLine, c.NewLine))
            .ToArray();
    }
}
