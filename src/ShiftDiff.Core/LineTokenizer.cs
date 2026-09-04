namespace ShiftDiff.Core;

public static class LineTokenizer {
  public static string[] Tokenize(string line) {
    if (line.Length == 0) {
      return [];
    }

    var tokens = new List<string>();
    var start = 0;
    var inWord = IsWordChar(line[0]);

    for (var i = 1; i < line.Length; i++) {
      var charIsWord = IsWordChar(line[i]);
      if (charIsWord != inWord) {
        tokens.Add(line[start..i]);
        start = i;
        inWord = charIsWord;
      }
    }

    tokens.Add(line[start..]);
    return tokens.ToArray();
  }

  public static string[] TokenizeSourceCode(string line) {
    if (line.Length == 0) {
      return [];
    }

    var tokens = new List<string>();
    var i = 0;

    while (i < line.Length) {
      if (IsLineCommentStart(line, i)) {
        tokens.Add(line[i..]);
        break;
      }

      if (IsQuoteChar(line[i])) {
        var literalEnd = FindLiteralEnd(line, i);
        tokens.Add(line[i..literalEnd]);
        i = literalEnd;
        continue;
      }

      var start = i;
      var inWord = IsWordChar(line[i]);
      i++;
      while (i < line.Length && !IsQuoteChar(line[i]) && !IsLineCommentStart(line, i) && IsWordChar(line[i]) == inWord) {
        i++;
      }

      tokens.Add(line[start..i]);
    }

    return tokens.ToArray();
  }

  private static int FindLiteralEnd(string line, int start) {
    var quote = line[start];
    var i = start + 1;

    while (i < line.Length) {
      if (line[i] == '\\' && i + 1 < line.Length) {
        i += 2;
        continue;
      }

      if (line[i] == quote) {
        return i + 1;
      }

      i++;
    }

    return line.Length;
  }

  private static bool IsLineCommentStart(string line, int i) =>
      i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/';

  private static bool IsQuoteChar(char c) => c is '"' or '\'';

  private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
