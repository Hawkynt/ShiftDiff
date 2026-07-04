using System.Text;

namespace ShiftDiff.Core.Tests;

public class FolderRenameDetectorTests
{
    private static byte[] Text(params string[] lines) => Encoding.UTF8.GetBytes(string.Join('\n', lines));

    [Fact]
    public void Detect_SimilarButEditedContentAtNewPath_MarksMovedEditedWithMovedFrom()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["old/file.txt"] = Text("alpha", "beta", "gamma", "delta", "epsilon"),
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["new/file.txt"] = Text("alpha", "beta", "gamma", "delta", "epsilon", "zeta"),
        };
        var changes = FolderMoveDetector.Detect(FolderComparer.Compare(baseFiles, targetFiles), baseFiles, targetFiles);

        var result = FolderRenameDetector.Detect(changes, baseFiles, targetFiles);

        var renamed = Assert.Single(result);
        Assert.Equal(FolderChangeType.MovedEdited, renamed.ChangeType);
        Assert.Equal("new/file.txt", renamed.RelativePath);
        Assert.Equal("old/file.txt", renamed.MovedFrom);
    }

    [Fact]
    public void Detect_TooDifferentContent_NoMatch_StaysAddedAndRemoved()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["old/file.txt"] = Text("alpha", "beta", "gamma"),
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["new/file.txt"] = Text("one", "two", "three", "four", "five", "six", "seven"),
        };
        var changes = FolderMoveDetector.Detect(FolderComparer.Compare(baseFiles, targetFiles), baseFiles, targetFiles);

        var result = FolderRenameDetector.Detect(changes, baseFiles, targetFiles);

        Assert.Equal(2, result.Length);
        Assert.Contains(result, e => e.RelativePath == "old/file.txt" && e.ChangeType == FolderChangeType.Removed);
        Assert.Contains(result, e => e.RelativePath == "new/file.txt" && e.ChangeType == FolderChangeType.Added);
    }

    [Fact]
    public void Detect_AmbiguousMultipleSimilarCandidates_LeavesAsAddedAndRemoved()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = Text("alpha", "beta", "gamma", "delta"),
            ["b.txt"] = Text("alpha", "beta", "gamma", "epsilon"),
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["c.txt"] = Text("alpha", "beta", "gamma", "zeta"),
        };
        var changes = FolderMoveDetector.Detect(FolderComparer.Compare(baseFiles, targetFiles), baseFiles, targetFiles);

        var result = FolderRenameDetector.Detect(changes, baseFiles, targetFiles);

        Assert.Equal(3, result.Length);
        Assert.All(result, e => Assert.NotEqual(FolderChangeType.MovedEdited, e.ChangeType));
    }

    [Fact]
    public void Detect_AlreadyExactlyMovedEntry_PassesThroughUnaffected()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["old/file.txt"] = Text("alpha", "beta", "gamma"),
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["new/file.txt"] = Text("alpha", "beta", "gamma"),
        };
        var changes = FolderMoveDetector.Detect(FolderComparer.Compare(baseFiles, targetFiles), baseFiles, targetFiles);

        var result = FolderRenameDetector.Detect(changes, baseFiles, targetFiles);

        var moved = Assert.Single(result);
        Assert.Equal(FolderChangeType.Moved, moved.ChangeType);
        Assert.Equal("old/file.txt", moved.MovedFrom);
    }

    [Fact]
    public void Detect_UnchangedAndChangedEntries_PassThroughUnaffected()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["same.txt"] = Text("alpha"),
            ["edited.txt"] = Text("alpha"),
        };
        var targetFiles = new Dictionary<string, byte[]>
        {
            ["same.txt"] = Text("alpha"),
            ["edited.txt"] = Text("beta"),
        };
        var changes = FolderMoveDetector.Detect(FolderComparer.Compare(baseFiles, targetFiles), baseFiles, targetFiles);

        var result = FolderRenameDetector.Detect(changes, baseFiles, targetFiles);

        Assert.Equal(2, result.Length);
        Assert.Contains(result, e => e.RelativePath == "same.txt" && e.ChangeType == FolderChangeType.Unchanged);
        Assert.Contains(result, e => e.RelativePath == "edited.txt" && e.ChangeType == FolderChangeType.Changed);
    }

    [Fact]
    public void Detect_NoRemainingAddedOrRemoved_ReturnsInputUnchanged()
    {
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["a.txt"] = Text("alpha"),
        };
        var targetFiles = new Dictionary<string, byte[]>(baseFiles);
        var changes = FolderComparer.Compare(baseFiles, targetFiles);

        var result = FolderRenameDetector.Detect(changes, baseFiles, targetFiles);

        Assert.Equal(changes, result);
    }
}
