namespace ShiftDiff.Core.Tests;

public class FolderMoveDetectorTests {
  [Fact]
  public void Detect_IdenticalContentAtNewPath_MarksMovedWithMovedFrom() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["old/file.txt"] = new byte[] { 1, 2, 3 },
    };
    var targetFiles = new Dictionary<string, byte[]> {
      ["new/file.txt"] = new byte[] { 1, 2, 3 },
    };
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderMoveDetector.Detect(changes, baseFiles, targetFiles);

    var moved = Assert.Single(result);
    Assert.Equal(FolderChangeType.Moved, moved.ChangeType);
    Assert.Equal("new/file.txt", moved.RelativePath);
    Assert.Equal("old/file.txt", moved.MovedFrom);
  }

  [Fact]
  public void Detect_DifferentContent_NoMoveDetected_StaysAddedAndRemoved() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["old/file.txt"] = new byte[] { 1, 2, 3 },
    };
    var targetFiles = new Dictionary<string, byte[]> {
      ["new/file.txt"] = new byte[] { 9, 9, 9 },
    };
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderMoveDetector.Detect(changes, baseFiles, targetFiles);

    Assert.Equal(2, result.Length);
    Assert.Contains(result, e => e.RelativePath == "old/file.txt" && e.ChangeType == FolderChangeType.Removed);
    Assert.Contains(result, e => e.RelativePath == "new/file.txt" && e.ChangeType == FolderChangeType.Added);
  }

  [Fact]
  public void Detect_AmbiguousMultipleCandidatesWithSameContent_LeavesAsAddedAndRemoved() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 1, 2, 3 },
      ["b.txt"] = new byte[] { 1, 2, 3 },
    };
    var targetFiles = new Dictionary<string, byte[]> {
      ["c.txt"] = new byte[] { 1, 2, 3 },
    };
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderMoveDetector.Detect(changes, baseFiles, targetFiles);

    Assert.Equal(3, result.Length);
    Assert.All(result, e => Assert.NotEqual(FolderChangeType.Moved, e.ChangeType));
  }

  [Fact]
  public void Detect_UnchangedAndChangedEntries_PassThroughUnaffected() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["same.txt"] = new byte[] { 1 },
      ["edited.txt"] = new byte[] { 1 },
    };
    var targetFiles = new Dictionary<string, byte[]> {
      ["same.txt"] = new byte[] { 1 },
      ["edited.txt"] = new byte[] { 2 },
    };
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderMoveDetector.Detect(changes, baseFiles, targetFiles);

    Assert.Equal(2, result.Length);
    Assert.Contains(result, e => e.RelativePath == "same.txt" && e.ChangeType == FolderChangeType.Unchanged);
    Assert.Contains(result, e => e.RelativePath == "edited.txt" && e.ChangeType == FolderChangeType.Changed);
  }

  [Fact]
  public void Detect_NoAddedOrRemoved_ReturnsInputUnchanged() {
    var baseFiles = new Dictionary<string, byte[]> {
      ["a.txt"] = new byte[] { 1 },
    };
    var targetFiles = new Dictionary<string, byte[]>(baseFiles);
    var changes = FolderComparer.Compare(baseFiles, targetFiles);

    var result = FolderMoveDetector.Detect(changes, baseFiles, targetFiles);

    Assert.Equal(changes, result);
  }
}
