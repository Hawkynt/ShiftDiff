namespace ShiftDiff.Core;

public static class SourceTokenizer {
  private sealed record LanguageProfile(
      HashSet<string> Keywords,
      string[] LineComments,
      (string Start, string End)[] BlockComments,
      string[] Quotes,
      bool VisualBasicStrings = false,
      bool CaseInsensitiveKeywords = false,
      bool SupportsSigils = false);

  private static readonly string[] Operators =
  [
      "??=", "===", "!==", "<<=", ">>=", "...", "::", "=>", "->", "==", "!=", "<=", ">=",
        "&&", "||", "??", "?.", "++", "--", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
        "<<", ">>", "**", "//", ":=", "=~", "!~", "..", "<&", ">&",
    ];

  private static readonly HashSet<char> Punctuation = ['(', ')', '{', '}', '[', ']', ',', ';', ':', '.'];

  private static readonly IReadOnlyDictionary<SourceLanguage, LanguageProfile> Profiles = BuildProfiles();

  public static IReadOnlyList<SourceToken> TokenizeLine(string line, SourceLanguage language) {
    if (language == SourceLanguage.PlainText || !Profiles.TryGetValue(language, out var profile)) {
      var start = 0;
      return LineTokenizer.Tokenize(line).Select(text => {
        var token = new SourceToken(
            string.IsNullOrWhiteSpace(text) ? SourceTokenKind.Whitespace : SourceTokenKind.Identifier,
            text,
            start);
        start += text.Length;
        return token;
      }).ToArray();
    }

    var result = new List<SourceToken>();
    var index = 0;

    while (index < line.Length) {
      var start = index;
      var character = line[index];

      if (char.IsWhiteSpace(character)) {
        while (index < line.Length && char.IsWhiteSpace(line[index])) index++;
        Add(SourceTokenKind.Whitespace);
        continue;
      }

      var lineComment = profile.LineComments.FirstOrDefault(marker => Matches(line, index, marker));
      if (lineComment is not null && IsValidLineComment(line, index, lineComment, language)) {
        index = line.Length;
        Add(SourceTokenKind.Comment);
        continue;
      }

      var blockComment = profile.BlockComments.FirstOrDefault(pair => Matches(line, index, pair.Start));
      if (blockComment.Start is not null) {
        var end = line.IndexOf(blockComment.End, index + blockComment.Start.Length, StringComparison.Ordinal);
        index = end < 0 ? line.Length : end + blockComment.End.Length;
        Add(SourceTokenKind.Comment);
        continue;
      }

      var quote = profile.Quotes.FirstOrDefault(marker => Matches(line, index, marker));
      if (quote is not null) {
        index = ConsumeString(line, index, quote, profile.VisualBasicStrings);
        Add(SourceTokenKind.String);
        continue;
      }

      if (char.IsLetter(character) || character == '_' || (profile.SupportsSigils && character is '$' or '@' or '%')) {
        index++;
        while (index < line.Length && (char.IsLetterOrDigit(line[index]) || line[index] == '_' || (profile.SupportsSigils && line[index] is '$' or '@' or '%'))) index++;
        var text = line[start..index];
        var lookup = profile.SupportsSigils ? text.TrimStart('$', '@', '%') : text;
        var comparison = profile.CaseInsensitiveKeywords ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var kind = profile.Keywords.Contains(lookup, comparison) ? SourceTokenKind.Keyword : SourceTokenKind.Identifier;
        result.Add(new SourceToken(kind, text, start));
        continue;
      }

      if (char.IsDigit(character)) {
        index++;
        while (index < line.Length && (char.IsLetterOrDigit(line[index]) || line[index] is '_' or '.')) index++;
        Add(SourceTokenKind.Number);
        continue;
      }

      var op = Operators.FirstOrDefault(candidate => Matches(line, index, candidate));
      if (op is not null) {
        index += op.Length;
        Add(SourceTokenKind.Operator);
        continue;
      }

      index++;
      Add(Punctuation.Contains(character) ? SourceTokenKind.Punctuation : SourceTokenKind.Operator);

      void Add(SourceTokenKind kind) => result.Add(new SourceToken(kind, line[start..index], start));
    }

    return result;
  }

  private static bool IsValidLineComment(string line, int index, string marker, SourceLanguage language) {
    if (language == SourceLanguage.VisualBasic && marker.Equals("REM", StringComparison.OrdinalIgnoreCase)) {
      var beforeIsBoundary = index == 0 || char.IsWhiteSpace(line[index - 1]);
      var after = index + marker.Length;
      var afterIsBoundary = after == line.Length || char.IsWhiteSpace(line[after]);
      return beforeIsBoundary && afterIsBoundary;
    }

    return true;
  }

  private static int ConsumeString(string line, int start, string quote, bool visualBasicStrings) {
    var index = start + quote.Length;
    while (index < line.Length) {
      if (visualBasicStrings && quote == "\"" && Matches(line, index, "\"\"")) {
        index += 2;
        continue;
      }

      if (Matches(line, index, quote)) {
        return index + quote.Length;
      }

      if (!visualBasicStrings && line[index] == '\\') {
        index += Math.Min(2, line.Length - index);
        continue;
      }

      index++;
    }

    return line.Length;
  }

  private static bool Matches(string text, int index, string value) =>
      index + value.Length <= text.Length && text.AsSpan(index, value.Length).SequenceEqual(value);

  private static IReadOnlyDictionary<SourceLanguage, LanguageProfile> BuildProfiles() {
    var cLikeComments = new[] { ("/*", "*/") };
    var cLikeQuotes = new[] { "\"", "'" };

    return new Dictionary<SourceLanguage, LanguageProfile> {
      [SourceLanguage.CSharp] = Profile("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly record ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while async await required init", ["//"], cLikeComments, cLikeQuotes),
      [SourceLanguage.JavaScript] = Profile("await break case catch class const continue debugger default delete do else export extends false finally for function if import in instanceof let new null return static super switch this throw true try typeof undefined var void while with yield async of", ["//"], cLikeComments, ["`", "\"", "'"]),
      [SourceLanguage.TypeScript] = Profile("abstract any as asserts bigint boolean break case catch class const constructor continue debugger declare default delete do else enum export extends false finally for from function get if implements import in infer instanceof interface is keyof let module namespace never new null number object of private protected public readonly require return set static string super switch symbol this throw true try type typeof undefined unique unknown var void while with yield async await", ["//"], cLikeComments, ["`", "\"", "'"]),
      [SourceLanguage.Java] = Profile("abstract assert boolean break byte case catch char class const continue default do double else enum extends false final finally float for goto if implements import instanceof int interface long native new null package private protected public return short static strictfp super switch synchronized this throw throws transient true try void volatile while record sealed permits var", ["//"], cLikeComments, cLikeQuotes),
      [SourceLanguage.C] = Profile("auto break case char const continue default do double else enum extern float for goto if inline int long register restrict return short signed sizeof static struct switch typedef union unsigned void volatile while _Bool _Complex _Imaginary", ["//"], cLikeComments, cLikeQuotes),
      [SourceLanguage.Cpp] = Profile("alignas alignof and and_eq asm auto bitand bitor bool break case catch char char8_t char16_t char32_t class compl concept const consteval constexpr constinit const_cast continue co_await co_return co_yield decltype default delete do double dynamic_cast else enum explicit export extern false float for friend goto if inline int long mutable namespace new noexcept not not_eq nullptr operator or or_eq private protected public register reinterpret_cast requires return short signed sizeof static static_assert static_cast struct switch template this thread_local throw true try typedef typeid typename union unsigned using virtual void volatile wchar_t while xor xor_eq", ["//"], cLikeComments, cLikeQuotes),
      [SourceLanguage.Python] = Profile("False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield match case", ["#"], [], ["\"\"\"", "'''", "\"", "'"]),
      [SourceLanguage.Go] = Profile("break default func interface select case defer go map struct chan else goto package switch const fallthrough if range type continue for import return var", ["//"], cLikeComments, ["`", "\"", "'"]),
      [SourceLanguage.Rust] = Profile("as async await break const continue crate dyn else enum extern false fn for if impl in let loop match mod move mut pub ref return self Self static struct super trait true type unsafe use where while abstract become box do final macro override priv typeof unsized virtual yield try union", ["//"], cLikeComments, cLikeQuotes),
      [SourceLanguage.Php] = Profile("__halt_compiler abstract and array as break callable case catch class clone const continue declare default die do echo else elseif empty enddeclare endfor endforeach endif endswitch endwhile enum eval exit extends final finally fn for foreach function global goto if implements include include_once instanceof insteadof interface isset list match namespace new or print private protected public readonly require require_once return static switch throw trait try unset use var while xor yield from true false null", ["//", "#"], cLikeComments, ["\"", "'"], supportsSigils: true),
      [SourceLanguage.VisualBasic] = Profile("AddHandler AddressOf Alias And AndAlso As Boolean ByRef Byte ByVal Call Case Catch CBool CByte CChar CDate CDbl CDec Char CInt Class CLng CObj Const Continue CSByte CShort CSng CStr CType CUInt CULng CUShort Date Decimal Declare Default Delegate Dim DirectCast Do Double Each Else ElseIf End Enum Erase Error Event Exit False Finally For Friend Function Get GetType GetXMLNamespace Global GoSub GoTo Handles If Implements Imports In Inherits Integer Interface Is IsNot Let Lib Like Long Loop Me Mod Module MustInherit MustOverride MyBase MyClass Namespace Narrowing New Next Not Nothing NotInheritable NotOverridable Object Of On Operator Option Optional Or OrElse Out Overloads Overridable Overrides ParamArray Partial Private Property Protected Public RaiseEvent ReadOnly ReDim REM RemoveHandler Resume Return SByte Select Set Shadows Shared Short Single Static Step Stop String Structure Sub SyncLock Then Throw To True Try TryCast TypeOf UInteger ULong UShort Using Variant Wend When While Widening With WithEvents WriteOnly Xor", ["'", "REM"], [], ["\""], visualBasicStrings: true, caseInsensitive: true),
      [SourceLanguage.Perl] = Profile("__DATA__ __END__ and cmp continue do else elsif eq exp for foreach ge given goto grep if last le local lt m map my ne next no not or our package print q qq qr qw qx redo require return s say state sub tr unless until use when while x xor y", ["#"], [], ["\"", "'", "`"], supportsSigils: true),
      [SourceLanguage.Ruby] = Profile("BEGIN END alias and begin break case class def defined do else elsif end ensure false for if in module next nil not or redo rescue retry return self super then true undef unless until when while yield", ["#"], [], ["\"", "'", "`"], supportsSigils: true),
      [SourceLanguage.Html] = Profile("", [], [("<!--", "-->")], ["\"", "'"]),
      [SourceLanguage.Css] = Profile("", ["//"], cLikeComments, ["\"", "'"]),
      [SourceLanguage.Sql] = Profile("add all alter and any as asc authorization backup begin between break browse bulk by cascade case check checkpoint close clustered coalesce collate column commit compute constraint contains containstable continue convert create cross current current_date current_time current_timestamp cursor database dbcc deallocate declare default delete deny desc disk distinct distributed double drop dump else end errlvl escape except exec execute exists exit external fetch file fillfactor for foreign freetext freetexttable from full function goto grant group having holdlock identity identity_insert identitycol if in index inner insert intersect into is join key kill left like lineno load merge national nocheck nonclustered not null nullif of off offsets on open opendatasource openquery openrowset openxml option or order outer over percent pivot plan precision primary print proc procedure public raiserror read readtext reconfigure references replication restore restrict return revert revoke right rollback rowcount rowguidcol rule save schema securityaudit select semantickeyphrasetable semanticsimilaritydetail semanticsimilaritytable session_user set setuser shutdown some statistics system_user table tablesample textsize then to top tran transaction trigger truncate try_convert tsequal union unique unpivot update updatetext use user values varying view waitfor when where while with within group writetext", ["--"], cLikeComments, ["\"", "'"], caseInsensitive: true),
    };
  }

  private static LanguageProfile Profile(
      string keywords,
      string[] lineComments,
      (string Start, string End)[] blockComments,
      string[] quotes,
      bool visualBasicStrings = false,
      bool caseInsensitive = false,
      bool supportsSigils = false) =>
      new(
          keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal),
          lineComments.OrderByDescending(value => value.Length).ToArray(),
          blockComments,
          quotes.OrderByDescending(value => value.Length).ToArray(),
          visualBasicStrings,
          caseInsensitive,
          supportsSigils);

  private static bool Contains(this HashSet<string> values, string value, StringComparer comparer) =>
      values.Comparer.Equals(comparer) ? values.Contains(value) : values.Any(candidate => comparer.Equals(candidate, value));
}
