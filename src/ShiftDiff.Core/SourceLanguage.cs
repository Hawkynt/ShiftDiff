namespace ShiftDiff.Core;

public enum SourceLanguage
{
    PlainText,
    CSharp,
    JavaScript,
    TypeScript,
    Java,
    C,
    Cpp,
    Python,
    Go,
    Rust,
    Php,
    VisualBasic,
    Perl,
    Ruby,
    Html,
    Css,
    Sql,
}

public enum SourceTokenKind
{
    Whitespace,
    Keyword,
    Identifier,
    Number,
    String,
    Comment,
    Operator,
    Punctuation,
}

public sealed record SourceToken(SourceTokenKind Kind, string Text, int Start);

