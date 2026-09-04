using System.Text;

namespace ShiftDiff.Core;

public static class LineNormalizer {
  public static string Trim(string line) => line.Trim();

  public static string NormalizeWhitespace(string line) {
    var builder = new StringBuilder(line.Length);
    var previousWasWhitespace = false;

    foreach (var character in line) {
      if (char.IsWhiteSpace(character)) {
        if (!previousWasWhitespace) {
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

  public static string RemoveWhitespace(string line) {
    var builder = new StringBuilder(line.Length);

    foreach (var character in line) {
      if (!char.IsWhiteSpace(character)) {
        builder.Append(character);
      }
    }

    return builder.ToString();
  }
}
