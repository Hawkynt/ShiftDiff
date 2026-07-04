namespace ShiftDiff.Core.Tests;

public class FolderChangeFilterTests
{
    private static FolderEntryChange[] SampleChanges() =>
        new[]
        {
            new FolderEntryChange("a.cs", FolderChangeType.Added),
            new FolderEntryChange("b.txt", FolderChangeType.Removed),
            new FolderEntryChange("nested/c.CS", FolderChangeType.Changed),
            new FolderEntryChange("d.md", FolderChangeType.Unchanged),
        };

    [Fact]
    public void ByExtension_KeepsOnlyMatchingExtension()
    {
        var result = FolderChangeFilter.ByExtension(SampleChanges(), ".txt");

        var kept = Assert.Single(result);
        Assert.Equal("b.txt", kept.RelativePath);
    }

    [Fact]
    public void ByExtension_MatchesCaseInsensitively()
    {
        var result = FolderChangeFilter.ByExtension(SampleChanges(), ".cs");

        Assert.Equal(
            new[] { "a.cs", "nested/c.CS" },
            result.Select(e => e.RelativePath));
    }

    [Fact]
    public void ByExtension_MultipleExtensions_KeepsAnyMatch()
    {
        var result = FolderChangeFilter.ByExtension(SampleChanges(), ".txt", ".md");

        Assert.Equal(
            new[] { "b.txt", "d.md" },
            result.Select(e => e.RelativePath));
    }

    [Fact]
    public void ByExtension_NoExtensionsGiven_ReturnsAllUnfiltered()
    {
        var changes = SampleChanges();

        var result = FolderChangeFilter.ByExtension(changes);

        Assert.Equal(changes, result);
    }

    [Fact]
    public void ByExtension_PreservesInputOrder()
    {
        var result = FolderChangeFilter.ByExtension(SampleChanges(), ".md", ".cs");

        Assert.Equal(
            new[] { "a.cs", "nested/c.CS", "d.md" },
            result.Select(e => e.RelativePath));
    }

    [Fact]
    public void ByPathPrefix_KeepsOnlyMatchingPrefix()
    {
        var result = FolderChangeFilter.ByPathPrefix(SampleChanges(), "nested/");

        var kept = Assert.Single(result);
        Assert.Equal("nested/c.CS", kept.RelativePath);
    }

    [Fact]
    public void ByPathPrefix_MultiplePrefixes_KeepsAnyMatch()
    {
        var result = FolderChangeFilter.ByPathPrefix(SampleChanges(), "nested/", "b.");

        Assert.Equal(
            new[] { "b.txt", "nested/c.CS" },
            result.Select(e => e.RelativePath));
    }

    [Fact]
    public void ByPathPrefix_NoPrefixesGiven_ReturnsAllUnfiltered()
    {
        var changes = SampleChanges();

        var result = FolderChangeFilter.ByPathPrefix(changes);

        Assert.Equal(changes, result);
    }

    [Fact]
    public void ByPathPrefix_IsCaseSensitive()
    {
        var result = FolderChangeFilter.ByPathPrefix(SampleChanges(), "Nested/");

        Assert.Empty(result);
    }

    [Fact]
    public void ByPathPrefix_PreservesInputOrder()
    {
        var result = FolderChangeFilter.ByPathPrefix(SampleChanges(), "d.", "a.");

        Assert.Equal(
            new[] { "a.cs", "d.md" },
            result.Select(e => e.RelativePath));
    }

    private static FolderEntryChange[] SizedSampleChanges() =>
        new[]
        {
            new FolderEntryChange("a.cs", FolderChangeType.Added, Size: 100),
            new FolderEntryChange("b.txt", FolderChangeType.Removed, Size: 50),
            new FolderEntryChange("nested/c.CS", FolderChangeType.Changed, Size: 200),
            new FolderEntryChange("d.md", FolderChangeType.Unchanged, Size: 10),
        };

    [Fact]
    public void BySize_MinOnly_KeepsEntriesAtOrAboveMin()
    {
        var result = FolderChangeFilter.BySize(SizedSampleChanges(), minSize: 100);

        Assert.Equal(
            new[] { "a.cs", "nested/c.CS" },
            result.Select(e => e.RelativePath));
    }

    [Fact]
    public void BySize_MaxOnly_KeepsEntriesAtOrBelowMax()
    {
        var result = FolderChangeFilter.BySize(SizedSampleChanges(), maxSize: 50);

        Assert.Equal(
            new[] { "b.txt", "d.md" },
            result.Select(e => e.RelativePath));
    }

    [Fact]
    public void BySize_MinAndMax_KeepsWithinRange()
    {
        var result = FolderChangeFilter.BySize(SizedSampleChanges(), minSize: 60, maxSize: 150);

        Assert.Equal(
            new[] { "a.cs" },
            result.Select(e => e.RelativePath));
    }

    [Fact]
    public void BySize_NoBoundsGiven_ReturnsAllUnfiltered()
    {
        var changes = SizedSampleChanges();

        var result = FolderChangeFilter.BySize(changes);

        Assert.Equal(changes, result);
    }

    [Fact]
    public void BySize_PreservesInputOrder()
    {
        var result = FolderChangeFilter.BySize(SizedSampleChanges(), minSize: 10);

        Assert.Equal(
            new[] { "a.cs", "b.txt", "nested/c.CS", "d.md" },
            result.Select(e => e.RelativePath));
    }
}
