using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

public class DiffSegmentBuilderTests {
  [Fact]
  public void Build_NullLine_ProducesNoSegments() {
    Assert.Empty(DiffSegmentBuilder.Build(null, null, false));
  }

  [Fact]
  public void Build_EmptyLine_ProducesNoSegments() {
    Assert.Empty(DiffSegmentBuilder.Build(string.Empty, null, false));
  }

  [Fact]
  public void Build_PlainLineWithoutTokenChanges_IsOneUnchangedSegment() {
    var segments = DiffSegmentBuilder.Build("hello world", null, false);

    var segment = Assert.Single(segments);
    Assert.Equal("hello world", segment.Text);
    Assert.Equal(DiffSegmentKind.Unchanged, segment.Kind);
  }

  [Fact]
  public void Build_SegmentsAlwaysReconstructTheOriginalLine() {
    const string line = "var total = count + 1; // done";
    var tokens = TokenDiffer.Diff(line, "var total = count + 2; // done", SourceLanguage.CSharp);

    var text = string.Concat(DiffSegmentBuilder.Build(line, tokens, true, SourceLanguage.CSharp).Select(s => s.Text));

    Assert.Equal(line, text);
  }

  [Fact]
  public void Build_NewSide_MarksTheReplacementTokenAsAdded() {
    var tokens = TokenDiffer.Diff("value = 1", "value = 2");

    var segments = DiffSegmentBuilder.Build("value = 2", tokens, oldSide: false);

    Assert.Contains(segments, segment => segment.Text == "2" && segment.Kind == DiffSegmentKind.Added);
    Assert.Contains(segments, segment => segment.Text.Contains("value") && segment.Kind == DiffSegmentKind.Unchanged);
  }

  [Fact]
  public void Build_OldSide_MarksTheReplacedTokenAsRemoved() {
    var tokens = TokenDiffer.Diff("value = 1", "value = 2");

    var segments = DiffSegmentBuilder.Build("value = 1", tokens, oldSide: true);

    Assert.Contains(segments, segment => segment.Text == "1" && segment.Kind == DiffSegmentKind.Removed);
  }

  [Fact]
  public void Build_OldSide_DoesNotMarkTokensThatOnlyExistInTheNewLine() {
    var tokens = TokenDiffer.Diff("a", "a b");

    var segments = DiffSegmentBuilder.Build("a", tokens, oldSide: true);

    Assert.All(segments, segment => Assert.Equal(DiffSegmentKind.Unchanged, segment.Kind));
  }

  [Fact]
  public void Build_WithLanguage_TagsKeywordsAndStringsForSyntaxColouring() {
    var segments = DiffSegmentBuilder.Build("if (x) return \"text\";", null, false, SourceLanguage.CSharp);

    Assert.Contains(segments, segment => segment.Syntax == SourceTokenKind.Keyword);
    Assert.Contains(segments, segment => segment.Syntax == SourceTokenKind.String);
  }

  [Fact]
  public void Build_PlainTextLanguage_ProducesNoSyntaxSplitting() {
    var segments = DiffSegmentBuilder.Build("if (x) return;", null, false, SourceLanguage.PlainText);

    Assert.Single(segments);
  }

  [Fact]
  public void Build_AdjacentSegmentsOfTheSameKind_AreMerged() {
    var segments = DiffSegmentBuilder.Build("aaa bbb ccc", null, false, SourceLanguage.PlainText);

    Assert.Single(segments);
  }

  [Fact]
  public void Build_WithFallbackKind_AppliesItToTheWholeLine() {
    var segments = DiffSegmentBuilder.Build("added line", null, false, SourceLanguage.PlainText, DiffSegmentKind.Added);

    Assert.All(segments, segment => Assert.Equal(DiffSegmentKind.Added, segment.Kind));
  }
}
