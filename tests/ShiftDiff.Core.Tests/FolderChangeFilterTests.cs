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
}
