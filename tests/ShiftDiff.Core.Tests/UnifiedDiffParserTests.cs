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
}
