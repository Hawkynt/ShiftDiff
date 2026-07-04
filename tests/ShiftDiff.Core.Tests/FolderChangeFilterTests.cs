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
}
