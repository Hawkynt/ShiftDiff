using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

public class FolderComparerTests
{
    [Fact]
    public void Compare_SamePathsSameContent_AllUnchanged()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1, 2, 3 },
            ["b.txt"] = new byte[] { 4, 5, 6 },
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1, 2, 3 },
            ["b.txt"] = new byte[] { 4, 5, 6 },
        };

        var result = FolderComparer.Compare(baseFiles, targetFiles);

        Assert.All(result, entry => Assert.Equal(FolderChangeType.Unchanged, entry.ChangeType));
    }

    [Fact]
    public void Compare_PathOnlyInTarget_MarksAdded()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1 },
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1 },
            ["new.txt"] = new byte[] { 9 },
        };

        var result = FolderComparer.Compare(baseFiles, targetFiles);

        var added = Assert.Single(result, e => e.RelativePath == "new.txt");
        Assert.Equal(FolderChangeType.Added, added.ChangeType);
    }

    [Fact]
    public void Compare_PathOnlyInBase_MarksRemoved()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1 },
            ["gone.txt"] = new byte[] { 2 },
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1 },
        };

        var result = FolderComparer.Compare(baseFiles, targetFiles);

        var removed = Assert.Single(result, e => e.RelativePath == "gone.txt");
        Assert.Equal(FolderChangeType.Removed, removed.ChangeType);
    }

    [Fact]
    public void Compare_SamePathDifferentContent_MarksChanged()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1, 2, 3 },
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1, 2, 4 },
        };

        var result = FolderComparer.Compare(baseFiles, targetFiles);

        var changed = Assert.Single(result);
        Assert.Equal(FolderChangeType.Changed, changed.ChangeType);
    }

    [Fact]
    public void Compare_ResultsOrderedByPath_Ordinal()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["Zeta.txt"] = new byte[] { 1 },
            ["alpha.txt"] = new byte[] { 2 },
            ["beta.txt"] = new byte[] { 3 },
        };
        var targetFiles = new Dictionary<string, byte[]>(baseFiles);

        var result = FolderComparer.Compare(baseFiles, targetFiles);

        Assert.Equal(
            new[] { "Zeta.txt", "alpha.txt", "beta.txt" },
            result.Select(e => e.RelativePath));
    }
}
