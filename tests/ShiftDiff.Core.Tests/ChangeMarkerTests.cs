using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

// FR-043: every marker must carry text/tooltip semantics, never emoji alone.
public class ChangeMarkerTests
{
    [Theory]
    [InlineData(ChangeType.Unchanged, " ")]
    [InlineData(ChangeType.Edited, "~")]
    [InlineData(ChangeType.Added, "+")]
    [InlineData(ChangeType.Removed, "-")]
    [InlineData(ChangeType.Moved, "M")]
    [InlineData(ChangeType.MovedEdited, "%")]
    [InlineData(ChangeType.Split, "S")]
    [InlineData(ChangeType.Merged, "J")]
    [InlineData(ChangeType.Uncertain, "?")]
    [InlineData(ChangeType.Conflict, "!")]
    public void Text_ForChangeType_ReturnsSingleAsciiMarker(ChangeType changeType, string expected)
    {
        Assert.Equal(expected, ChangeMarker.Text(changeType));
    }

    [Fact]
    public void Emoji_ForEveryChangeType_ReturnsNonEmptyDistinctGlyph()
    {
        var glyphs = Enum.GetValues<ChangeType>().Select(ChangeMarker.Emoji).ToArray();

        Assert.All(glyphs, glyph => Assert.False(string.IsNullOrWhiteSpace(glyph)));
        Assert.Equal(glyphs.Length, glyphs.Distinct().Count());
    }

    [Fact]
    public void Label_ForEveryChangeType_ReturnsHumanReadableText()
    {
        Assert.Equal("moved + edited", ChangeMarker.Label(ChangeType.MovedEdited));
        Assert.Equal("added", ChangeMarker.Label(ChangeType.Added));
        Assert.All(Enum.GetValues<ChangeType>(), type => Assert.False(string.IsNullOrWhiteSpace(ChangeMarker.Label(type))));
    }

    [Fact]
    public void For_WithEmojiDisabled_FallsBackToTextMarker()
    {
        Assert.Equal("+", ChangeMarker.For(ChangeType.Added, useEmoji: false));
        Assert.Equal(ChangeMarker.Emoji(ChangeType.Added), ChangeMarker.For(ChangeType.Added, useEmoji: true));
    }

    [Theory]
    [InlineData(Confidence.Certain, "certain")]
    [InlineData(Confidence.Likely, "likely")]
    [InlineData(Confidence.Possible, "possible")]
    [InlineData(Confidence.Weak, "weak")]
    [InlineData(Confidence.Rejected, "rejected")]
    public void ConfidenceLabel_ReturnsLowercaseSpecVocabulary(Confidence confidence, string expected)
    {
        Assert.Equal(expected, ChangeMarker.Label(confidence));
    }
}
