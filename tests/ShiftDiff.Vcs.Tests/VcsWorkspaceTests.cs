using System.Text;
using ShiftDiff.Vcs;

namespace ShiftDiff.Vcs.Tests;

// AC-006/AC-007: the workspace is what the UI and CLI actually consume — a
// changed-file list plus old/new bytes ready for the diff engine.
public class VcsWorkspaceTests {
  [Fact]
  public void ListChanges_DropsIgnoredAndUnchangedEntries() {
    var provider = new StubProvider {
      Changes =
        [
            new VcsFileStatus("kept.cs", VcsChangeKind.Modified),
                new VcsFileStatus("bin/app.dll", VcsChangeKind.Ignored),
                new VcsFileStatus("same.cs", VcsChangeKind.Unchanged),
            ],
    };

    var changes = new VcsWorkspace(provider, "/repo").ListChanges();

    Assert.Single(changes);
    Assert.Equal("kept.cs", changes[0].Path);
  }

  [Fact]
  public void ListChanges_DeduplicatesTheSamePathAndKindReportedTwice() {
    var provider = new StubProvider {
      Changes =
        [
            new VcsFileStatus("f.cs", VcsChangeKind.Modified, Staged: true),
                new VcsFileStatus("f.cs", VcsChangeKind.Modified, Staged: false),
            ],
    };

    Assert.Single(new VcsWorkspace(provider, "/repo").ListChanges());
  }

  [Fact]
  public void Load_ModifiedFile_ReadsBothSidesAtTheRequestedRevisions() {
    var provider = new StubProvider { Contents = { ["HEAD:f.cs"] = "old", [":f.cs"] = "new" } };

    var comparison = new VcsWorkspace(provider, "/repo")
        .Load(new VcsFileStatus("f.cs", VcsChangeKind.Modified), "HEAD", "");

    Assert.Equal("old", Encoding.UTF8.GetString(comparison.OldContent));
    Assert.Equal("new", Encoding.UTF8.GetString(comparison.NewContent));
    Assert.Equal("f.cs@HEAD", comparison.OldPath);
    Assert.Equal("f.cs", comparison.NewPath);
  }

  [Fact]
  public void Load_AddedFile_TreatsTheOldSideAsEmptyWithoutQueryingIt() {
    var provider = new StubProvider { Contents = { [":new.cs"] = "fresh" } };

    var comparison = new VcsWorkspace(provider, "/repo")
        .Load(new VcsFileStatus("new.cs", VcsChangeKind.Added), "HEAD", "");

    Assert.Empty(comparison.OldContent);
    Assert.Equal("fresh", Encoding.UTF8.GetString(comparison.NewContent));
  }

  [Fact]
  public void Load_DeletedFile_TreatsTheNewSideAsEmpty() {
    var provider = new StubProvider { Contents = { ["HEAD:gone.cs"] = "was here" } };

    var comparison = new VcsWorkspace(provider, "/repo")
        .Load(new VcsFileStatus("gone.cs", VcsChangeKind.Deleted), "HEAD", "");

    Assert.Equal("was here", Encoding.UTF8.GetString(comparison.OldContent));
    Assert.Empty(comparison.NewContent);
  }

  [Fact]
  public void Load_RenamedFile_ReadsTheOldSideFromItsOriginalPath() {
    var provider = new StubProvider { Contents = { ["HEAD:old.cs"] = "content", [":new.cs"] = "content" } };

    var comparison = new VcsWorkspace(provider, "/repo")
        .Load(new VcsFileStatus("new.cs", VcsChangeKind.Renamed, OriginalPath: "old.cs"), "HEAD", "");

    Assert.Equal("content", Encoding.UTF8.GetString(comparison.OldContent));
    Assert.Equal("old.cs@HEAD", comparison.OldPath);
  }

  [Fact]
  public void Open_PathWithoutAnyRepository_ReturnsNull() {
    var directory = Path.Combine(Path.GetTempPath(), "shiftdiff-vcs", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      Assert.Null(VcsWorkspace.Open(directory, [new StubProvider()]));
    } finally {
      Directory.Delete(directory, recursive: true);
    }
  }

  private sealed class StubProvider : IVcsProvider {
    public VcsKind Kind => VcsKind.Git;

    public List<VcsFileStatus> Changes { get; init; } = [];

    public Dictionary<string, string> Contents { get; init; } = [];

    public RepositoryInfo? Detect(string path) => null;

    public IReadOnlyList<VcsFileStatus> GetWorkingChanges(string root) => Changes;

    public IReadOnlyList<VcsFileStatus> GetChanges(string root, string fromRevision, string toRevision) => Changes;

    public string GetFileContent(string root, string relativePath, string revision) =>
        Contents.GetValueOrDefault($"{revision}:{relativePath}", string.Empty);

    public IReadOnlyList<VcsRevision> GetHistory(string root, string? relativePath = null, int limit = 50) => [];
  }
}
