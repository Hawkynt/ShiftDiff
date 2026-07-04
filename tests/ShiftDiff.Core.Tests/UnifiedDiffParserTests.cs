using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class UnifiedDiffParserTests
{
    [Fact]
    public void BothCountsExplicit_ParsesAllFourValues()
    {
        var header = UnifiedDiffParser.ParseHunkHeader("@@ -1,5 +1,6 @@");
        Assert.Equal(1, header.OldStart);
        Assert.Equal(5, header.OldCount);
        Assert.Equal(1, header.NewStart);
        Assert.Equal(6, header.NewCount);
    }

    [Fact]
    public void OmittedCount_DefaultsToOne()
    {
        var header = UnifiedDiffParser.ParseHunkHeader("@@ -1 +1 @@");
        Assert.Equal(1, header.OldStart);
        Assert.Equal(1, header.OldCount);
        Assert.Equal(1, header.NewStart);
        Assert.Equal(1, header.NewCount);
    }

    [Fact]
    public void ZeroOldCount_ParsesNewFileAddition()
    {
        var header = UnifiedDiffParser.ParseHunkHeader("@@ -0,0 +1,3 @@");
        Assert.Equal(0, header.OldStart);
        Assert.Equal(0, header.OldCount);
        Assert.Equal(1, header.NewStart);
        Assert.Equal(3, header.NewCount);
    }

    [Fact]
    public void TrailingSectionHeading_IsIgnored()
    {
        var header = UnifiedDiffParser.ParseHunkHeader("@@ -10,3 +10,4 @@ public void Foo()");
        Assert.Equal(10, header.OldStart);
        Assert.Equal(3, header.OldCount);
        Assert.Equal(10, header.NewStart);
        Assert.Equal(4, header.NewCount);
    }

    [Fact]
    public void MissingHunkMarkers_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseHunkHeader("not a hunk header"));
    }

    [Fact]
    public void PlainPaths_NoSuffix_ParsesBothPaths()
    {
        var header = UnifiedDiffParser.ParseFileHeader("--- old.txt", "+++ new.txt");
        Assert.Equal("old.txt", header.SourcePath);
        Assert.Equal("new.txt", header.TargetPath);
    }

    [Fact]
    public void TabSeparatedTimestampSuffix_IsStripped()
    {
        var header = UnifiedDiffParser.ParseFileHeader(
            "--- old.txt\t2024-01-01 00:00:00",
            "+++ new.txt\t2024-01-02 00:00:00");
        Assert.Equal("old.txt", header.SourcePath);
        Assert.Equal("new.txt", header.TargetPath);
    }

    [Fact]
    public void GitStylePrefix_IsKeptVerbatim()
    {
        var header = UnifiedDiffParser.ParseFileHeader("--- a/src/Foo.cs", "+++ b/src/Foo.cs");
        Assert.Equal("a/src/Foo.cs", header.SourcePath);
        Assert.Equal("b/src/Foo.cs", header.TargetPath);
    }

    [Fact]
    public void MalformedOldLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseFileHeader("old.txt", "+++ new.txt"));
    }

    [Fact]
    public void MalformedNewLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseFileHeader("--- old.txt", "new.txt"));
    }

    [Fact]
    public void AddedLine_ParsesKindAndContent()
    {
        var line = UnifiedDiffParser.ParseLine("+new text");
        Assert.Equal(UnifiedDiffLineKind.Added, line.Kind);
        Assert.Equal("new text", line.Content);
    }

    [Fact]
    public void RemovedLine_ParsesKindAndContent()
    {
        var line = UnifiedDiffParser.ParseLine("-old text");
        Assert.Equal(UnifiedDiffLineKind.Removed, line.Kind);
        Assert.Equal("old text", line.Content);
    }

    [Fact]
    public void ContextLine_ParsesKindAndContent()
    {
        var line = UnifiedDiffParser.ParseLine(" unchanged text");
        Assert.Equal(UnifiedDiffLineKind.Context, line.Kind);
        Assert.Equal("unchanged text", line.Content);
    }

    [Fact]
    public void BareMarker_ParsesEmptyContent()
    {
        var line = UnifiedDiffParser.ParseLine("+");
        Assert.Equal(UnifiedDiffLineKind.Added, line.Kind);
        Assert.Equal("", line.Content);
    }

    [Fact]
    public void UnrecognizedPrefix_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseLine("@oops"));
    }

    [Fact]
    public void EmptyString_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseLine(""));
    }
}
