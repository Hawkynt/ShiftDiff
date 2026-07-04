namespace ShiftDiff.Core.Tests;

public class FolderCopyDetectorTests
{
    [Fact]
    public void Detect_AddedFileMatchesStillPresentSourceContent_MarksCopiedFrom()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["original.txt"] = new byte[] { 1, 2, 3 },
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["original.txt"] = new byte[] { 1, 2, 3 },
            ["duplicate.txt"] = new byte[] { 1, 2, 3 },
        };
        var changes = FolderComparer.Compare(baseFiles, targetFiles);

        var result = FolderCopyDetector.Detect(changes, baseFiles, targetFiles);

        var added = result.Single(e => e.RelativePath == "duplicate.txt");
        Assert.Equal(FolderChangeType.Added, added.ChangeType);
        Assert.Equal("original.txt", added.CopiedFrom);
        var source = result.Single(e => e.RelativePath == "original.txt");
        Assert.Equal(FolderChangeType.Unchanged, source.ChangeType);
    }

    [Fact]
    public void Detect_AddedFileMatchesOnlyRemovedContent_LeavesForMoveDetectorNotCopy()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["old/file.txt"] = new byte[] { 1, 2, 3 },
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["new/file.txt"] = new byte[] { 1, 2, 3 },
        };
        var changes = FolderComparer.Compare(baseFiles, targetFiles);

        var result = FolderCopyDetector.Detect(changes, baseFiles, targetFiles);

        var added = result.Single(e => e.RelativePath == "new/file.txt");
        Assert.Equal(FolderChangeType.Added, added.ChangeType);
        Assert.Null(added.CopiedFrom);
    }

    [Fact]
    public void Detect_AmbiguousMultipleStillPresentSources_LeavesCopiedFromNull()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1, 2, 3 },
            ["b.txt"] = new byte[] { 1, 2, 3 },
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1, 2, 3 },
            ["b.txt"] = new byte[] { 1, 2, 3 },
            ["c.txt"] = new byte[] { 1, 2, 3 },
        };
        var changes = FolderComparer.Compare(baseFiles, targetFiles);

        var result = FolderCopyDetector.Detect(changes, baseFiles, targetFiles);

        var added = result.Single(e => e.RelativePath == "c.txt");
        Assert.Null(added.CopiedFrom);
    }

    [Fact]
    public void Detect_AlreadyMovedEntry_PassesThroughUnaffected()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["old/file.txt"] = new byte[] { 1, 2, 3 },
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["new/file.txt"] = new byte[] { 1, 2, 3 },
        };
        var changes = FolderMoveDetector.Detect(FolderComparer.Compare(baseFiles, targetFiles), baseFiles, targetFiles);

        var result = FolderCopyDetector.Detect(changes, baseFiles, targetFiles);

        var moved = Assert.Single(result);
        Assert.Equal(FolderChangeType.Moved, moved.ChangeType);
        Assert.Equal("old/file.txt", moved.MovedFrom);
        Assert.Null(moved.CopiedFrom);
    }

    [Fact]
    public void Detect_NoAddedEntries_ReturnsInputUnchanged()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = new byte[] { 1 },
        };
        var targetFiles = new Dictionary<string, byte[]>(baseFiles);
        var changes = FolderComparer.Compare(baseFiles, targetFiles);

        var result = FolderCopyDetector.Detect(changes, baseFiles, targetFiles);

        Assert.Equal(changes, result);
    }
}
