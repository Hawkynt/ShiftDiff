namespace ShiftDiff.Core;

public static class TokenDiffer
{
    public static TokenChange[] Diff(string oldLine, string newLine, bool ignoreCase = false, WhitespaceMode whitespaceMode = WhitespaceMode.None, bool isSourceCode = false)
    {
        var oldTokens = isSourceCode ? LineTokenizer.TokenizeSourceCode(oldLine) : LineTokenizer.Tokenize(oldLine);
        var newTokens = isSourceCode ? LineTokenizer.TokenizeSourceCode(newLine) : LineTokenizer.Tokenize(newLine);
        return DiffTokens(oldTokens, newTokens, ignoreCase, whitespaceMode);
    }

    public static TokenChange[] Diff(
        string oldLine,
        string newLine,
        SourceLanguage language,
        bool ignoreCase = false,
        WhitespaceMode whitespaceMode = WhitespaceMode.None)
    {
        var oldTokens = SourceTokenizer.TokenizeLine(oldLine, language).Select(token => token.Text).ToArray();
        var newTokens = SourceTokenizer.TokenizeLine(newLine, language).Select(token => token.Text).ToArray();
        return DiffTokens(oldTokens, newTokens, ignoreCase, whitespaceMode);
    }

    private static TokenChange[] DiffTokens(string[] oldTokens, string[] newTokens, bool ignoreCase, WhitespaceMode whitespaceMode) =>
        LineDiffer.Diff(oldTokens, newTokens, ignoreCase, whitespaceMode)
            .Select(change => new TokenChange(change.ChangeType, change.OldLine, change.NewLine))
            .ToArray();
}

