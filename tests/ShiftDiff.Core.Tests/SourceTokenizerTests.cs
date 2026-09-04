using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

public class SourceTokenizerTests {
  [Theory]
  [InlineData(SourceLanguage.CSharp, "public sealed class Example // note", "public", "// note")]
  [InlineData(SourceLanguage.Perl, "my $value = 1; # note", "my", "# note")]
  [InlineData(SourceLanguage.Python, "def run(value): # note", "def", "# note")]
  [InlineData(SourceLanguage.Php, "public function run() { // note", "public", "// note")]
  [InlineData(SourceLanguage.Go, "func run() { // note", "func", "// note")]
  [InlineData(SourceLanguage.Rust, "pub fn run() { // note", "pub", "// note")]
  [InlineData(SourceLanguage.C, "static int run(void) { /* note */", "static", "/* note */")]
  [InlineData(SourceLanguage.Cpp, "constexpr auto run() { // note", "constexpr", "// note")]
  [InlineData(SourceLanguage.VisualBasic, "Public Sub Run() ' note", "Public", "' note")]
  [InlineData(SourceLanguage.Ruby, "def run(value) # note", "def", "# note")]
  public void TokenizeLine_RecognizesLanguageKeywordsAndComments(
      SourceLanguage language,
      string source,
      string keyword,
      string comment) {
    var tokens = SourceTokenizer.TokenizeLine(source, language);

    Assert.Contains(tokens, token => token.Kind == SourceTokenKind.Keyword && token.Text == keyword);
    Assert.Contains(tokens, token => token.Kind == SourceTokenKind.Comment && token.Text == comment);
  }

  [Theory]
  [InlineData(SourceLanguage.CSharp, "var url = \"https://example.test/a//b\";")]
  [InlineData(SourceLanguage.Python, "url = 'https://example.test/#fragment'")]
  [InlineData(SourceLanguage.VisualBasic, "Dim text = \"a ''quoted'' value\"")]
  [InlineData(SourceLanguage.Ruby, "url = \"https://example.test/#fragment\"")]
  public void TokenizeLine_DoesNotStartCommentsInsideStrings(SourceLanguage language, string source) {
    var tokens = SourceTokenizer.TokenizeLine(source, language);

    Assert.DoesNotContain(tokens, token => token.Kind == SourceTokenKind.Comment);
    Assert.Contains(tokens, token => token.Kind == SourceTokenKind.String);
  }

  [Theory]
  [InlineData(SourceLanguage.CSharp, "var answer = 42; // yes")]
  [InlineData(SourceLanguage.Perl, "my $answer = 42; # yes")]
  [InlineData(SourceLanguage.Python, "answer = 42 # yes")]
  [InlineData(SourceLanguage.Php, "$answer = 42; // yes")]
  [InlineData(SourceLanguage.Go, "answer := 42 // yes")]
  [InlineData(SourceLanguage.Rust, "let answer = 42; // yes")]
  [InlineData(SourceLanguage.C, "int answer = 42; /* yes */")]
  [InlineData(SourceLanguage.Cpp, "auto answer = 42; // yes")]
  [InlineData(SourceLanguage.VisualBasic, "Dim answer = 42 ' yes")]
  [InlineData(SourceLanguage.Ruby, "answer = 42 # yes")]
  public void TokenizeLine_PreservesEverySourceCharacter(SourceLanguage language, string source) {
    var reconstructed = string.Concat(SourceTokenizer.TokenizeLine(source, language).Select(token => token.Text));

    Assert.Equal(source, reconstructed);
  }

  [Fact]
  public void TokenizeLine_VisualBasicKeywordsAreCaseInsensitive() {
    var tokens = SourceTokenizer.TokenizeLine("public sub Run()", SourceLanguage.VisualBasic);

    Assert.Contains(tokens, token => token.Kind == SourceTokenKind.Keyword && token.Text == "public");
    Assert.Contains(tokens, token => token.Kind == SourceTokenKind.Keyword && token.Text == "sub");
  }
}

