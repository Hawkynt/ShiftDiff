namespace ShiftDiff.Core.Tests;

public class FolderCopyDetectorTests {
  [Fact]
  public void Detect_ContentDuplicatedToNewPath_OriginalStillUnchanged_MarksCopiedWithCopiedFrom() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 1, 2, 3 },
    };
    var targetFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 1, 2, 3 },
      ["copy.txt"] = new byte[] { 1, 2, 3 },
    };
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderCopyDetector.Detect(changes, targetFiles);

    var copy = result.Single(e => e.RelativePath == "copy.txt");
    Assert.Equal(FolderChangeType.Copied, copy.ChangeType);
    Assert.Equal("a.txt", copy.CopiedFrom);
    var source = result.Single(e => e.RelativePath == "a.txt");
    Assert.Equal(FolderChangeType.Unchanged, source.ChangeType);
  }

  [Fact]
  public void Detect_OriginalWasChanged_StillDetectsCopyFromChangedEntry() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 9, 9, 9 },
    };
    var targetFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 1, 2, 3 },
      ["copy.txt"] = new byte[] { 1, 2, 3 },
    };
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderCopyDetector.Detect(changes, targetFiles);

    var copy = result.Single(e => e.RelativePath == "copy.txt");
    Assert.Equal(FolderChangeType.Copied, copy.ChangeType);
    Assert.Equal("a.txt", copy.CopiedFrom);
    var source = result.Single(e => e.RelativePath == "a.txt");
    Assert.Equal(FolderChangeType.Changed, source.ChangeType);
  }

  [Fact]
  public void Detect_NoMatchingContent_StaysAdded() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 9, 9, 9 },
    };
    var targetFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 9, 9, 9 },
      ["new.txt"] = new byte[] { 1, 2, 3 },
    };
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderCopyDetector.Detect(changes, targetFiles);

    var added = result.Single(e => e.RelativePath == "new.txt");
    Assert.Equal(FolderChangeType.Added, added.ChangeType);
    Assert.Null(added.CopiedFrom);
  }

  [Fact]
  public void Detect_AmbiguousMultipleMatchingSources_LeavesAsAdded() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 1, 2, 3 },
      ["b.txt"] = new byte[] { 1, 2, 3 },
    };
    var targetFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 1, 2, 3 },
      ["b.txt"] = new byte[] { 1, 2, 3 },
      ["c.txt"] = new byte[] { 1, 2, 3 },
    };
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderCopyDetector.Detect(changes, targetFiles);

    var added = result.Single(e => e.RelativePath == "c.txt");
    Assert.Equal(FolderChangeType.Added, added.ChangeType);
    Assert.Null(added.CopiedFrom);
  }

  [Fact]
  public void Detect_AlreadyMovedEntry_PassesThroughUnaffected() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["old/file.txt"] = new byte[] { 1, 2, 3 },
    };
    var targetFiles = new Dictionary<string, byte[]> {
      ["new/file.txt"] = new byte[] { 1, 2, 3 },
    };
    var changes = FolderMoveDetector.Detect(FolderComparer.Compare(baseFiles, targetFiles), baseFiles, targetFiles);

    var result = FolderCopyDetector.Detect(changes, targetFiles);

    var moved = Assert.Single(result);
    Assert.Equal(FolderChangeType.Moved, moved.ChangeType);
    Assert.Equal("old/file.txt", moved.MovedFrom);
    Assert.Null(moved.CopiedFrom);
  }

  [Fact]
  public void Detect_TwoNewFilesWithIdenticalContent_BothStayAdded_NotMutuallyCopied() {
    var baseFiles = new Dictionary<string, byte[]>();
    var targetFiles = new Dictionary<string, byte[]> {
      ["x.txt"] = new byte[] { 1, 2, 3 },
      ["y.txt"] = new byte[] { 1, 2, 3 },
    };
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderCopyDetector.Detect(changes, targetFiles);

    var x = result.Single(e => e.RelativePath == "x.txt");
    var y = result.Single(e => e.RelativePath == "y.txt");
    Assert.Equal(FolderChangeType.Added, x.ChangeType);
    Assert.Null(x.CopiedFrom);
    Assert.Equal(FolderChangeType.Added, y.ChangeType);
    Assert.Null(y.CopiedFrom);
  }

  [Fact]
  public void Detect_NoAddedEntries_ReturnsInputUnchanged() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 1 },
    };
    var targetFiles = new Dictionary<string, byte[]>(baseFiles);
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderCopyDetector.Detect(changes, targetFiles);

    Assert.Equal(changes, result);
  }
}
