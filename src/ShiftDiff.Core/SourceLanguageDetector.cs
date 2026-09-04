using System.Text.RegularExpressions;

namespace ShiftDiff.Core;

public static partial class SourceLanguageDetector {
  private static readonly IReadOnlyDictionary<string, SourceLanguage> LanguagesByExtension =
      new Dictionary<string, SourceLanguage>(StringComparer.OrdinalIgnoreCase) {
        [".cs"] = SourceLanguage.CSharp,
        [".csx"] = SourceLanguage.CSharp,
        [".js"] = SourceLanguage.JavaScript,
        [".jsx"] = SourceLanguage.JavaScript,
        [".mjs"] = SourceLanguage.JavaScript,
        [".cjs"] = SourceLanguage.JavaScript,
        [".ts"] = SourceLanguage.TypeScript,
        [".tsx"] = SourceLanguage.TypeScript,
        [".java"] = SourceLanguage.Java,
        [".c"] = SourceLanguage.C,
        [".h"] = SourceLanguage.C,
        [".cc"] = SourceLanguage.Cpp,
        [".cpp"] = SourceLanguage.Cpp,
        [".cxx"] = SourceLanguage.Cpp,
        [".hh"] = SourceLanguage.Cpp,
        [".hpp"] = SourceLanguage.Cpp,
        [".hxx"] = SourceLanguage.Cpp,
        [".py"] = SourceLanguage.Python,
        [".pyw"] = SourceLanguage.Python,
        [".go"] = SourceLanguage.Go,
        [".rs"] = SourceLanguage.Rust,
        [".php"] = SourceLanguage.Php,
        [".php3"] = SourceLanguage.Php,
        [".php4"] = SourceLanguage.Php,
        [".php5"] = SourceLanguage.Php,
        [".phtml"] = SourceLanguage.Php,
        [".vb"] = SourceLanguage.VisualBasic,
        [".vbs"] = SourceLanguage.VisualBasic,
        [".pl"] = SourceLanguage.Perl,
        [".pm"] = SourceLanguage.Perl,
        [".t"] = SourceLanguage.Perl,
        [".rb"] = SourceLanguage.Ruby,
        [".rake"] = SourceLanguage.Ruby,
        [".gemspec"] = SourceLanguage.Ruby,
        [".html"] = SourceLanguage.Html,
        [".htm"] = SourceLanguage.Html,
        [".xhtml"] = SourceLanguage.Html,
        [".css"] = SourceLanguage.Css,
        [".scss"] = SourceLanguage.Css,
        [".less"] = SourceLanguage.Css,
        [".sql"] = SourceLanguage.Sql,
      };

  public static SourceLanguage Detect(string? path, string? content = null) {
    if (!string.IsNullOrWhiteSpace(path)) {
      var extension = Path.GetExtension(path);
      if (LanguagesByExtension.TryGetValue(extension, out var language)) {
        return language;
      }

      var fileName = Path.GetFileName(path);
      if (fileName.Equals("Rakefile", StringComparison.OrdinalIgnoreCase)
          || fileName.Equals("Gemfile", StringComparison.OrdinalIgnoreCase)) {
        return SourceLanguage.Ruby;
      }
    }

    if (string.IsNullOrWhiteSpace(content)) {
      return SourceLanguage.PlainText;
    }

    var lineEnd = content.IndexOfAny(['\r', '\n']);
    var firstLine = content.AsSpan(0, lineEnd >= 0 ? lineEnd : content.Length).Trim();
    if (firstLine.StartsWith("#!")) {
      var shebang = firstLine.ToString();
      if (ShebangContains(shebang, "python")) return SourceLanguage.Python;
      if (ShebangContains(shebang, "ruby")) return SourceLanguage.Ruby;
      if (ShebangContains(shebang, "perl")) return SourceLanguage.Perl;
      if (ShebangContains(shebang, "node") || ShebangContains(shebang, "deno")) return SourceLanguage.JavaScript;
    }

    if (content.AsSpan().TrimStart().StartsWith("<?php", StringComparison.OrdinalIgnoreCase)) {
      return SourceLanguage.Php;
    }

    return SourceLanguage.PlainText;
  }

  public static SourceLanguage DetectCommon(string? oldPath, string oldContent, string? newPath, string newContent) {
    var oldLanguage = Detect(oldPath, oldContent);
    var newLanguage = Detect(newPath, newContent);

    if (oldLanguage == newLanguage) return oldLanguage;
    if (oldLanguage == SourceLanguage.PlainText) return newLanguage;
    if (newLanguage == SourceLanguage.PlainText) return oldLanguage;
    return SourceLanguage.PlainText;
  }

  public static string GetDisplayName(SourceLanguage language) => language switch {
    SourceLanguage.CSharp => "C#",
    SourceLanguage.Cpp => "C++",
    SourceLanguage.VisualBasic => "Visual Basic",
    SourceLanguage.JavaScript => "JavaScript",
    SourceLanguage.TypeScript => "TypeScript",
    SourceLanguage.PlainText => "Plain text",
    _ => language.ToString(),
  };

  private static bool ShebangContains(string line, string runtime) =>
      Regex.IsMatch(line, $@"(?:^|[/\s]){Regex.Escape(runtime)}(?:[\d.]*)(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
