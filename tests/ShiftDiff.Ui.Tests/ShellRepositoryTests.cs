using ShiftDiff.Ui;
using ShiftDiff.Vcs;

namespace ShiftDiff.Ui.Tests;

// FR-030/FR-031: a repository session must be able to move between revisions,
// not only compare the working tree against HEAD.
public class ShellRepositoryTests : IDisposable {
  private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-repo", Guid.NewGuid().ToString("N"));

  public ShellRepositoryTests() => Directory.CreateDirectory(_root);

  public void Dispose() {
    if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
  }

  [Fact]
  public async Task OpenRepositoryAsync_OnAPlainFolder_LeavesTheSessionWithoutARepository() {
    var shell = new ShellViewModel();

    await shell.OpenRepositoryAsync(_root);

    Assert.False(shell.IsRepositorySession);
    Assert.Contains("No Git or SVN repository", shell.StatusText);
  }

  [Fact]
  public async Task OpenRevisionRangeAsync_WithoutARepository_ExplainsWhatIsMissing() {
    var shell = new ShellViewModel();

    await shell.OpenRevisionRangeAsync("v1", "v2");

    Assert.Contains("Open a repository first", shell.StatusText);
  }

  [Fact]
  public async Task OpenRepositoryAsync_OnAGitRepository_ComparesHeadAgainstTheWorkingTree() {
    var shell = new ShellViewModel();
    var repository = FakeGitRepository();

    await shell.OpenRepositoryAsync(repository);

    Assert.True(shell.IsRepositorySession);
    Assert.Equal(VcsRevisions.Head, shell.FromRevision);
    Assert.Equal(VcsRevisions.WorkingTree, shell.ToRevision);
  }

  [Fact]
  public async Task OpenRevisionRangeAsync_RecordsTheNewRange() {
    var shell = new ShellViewModel();
    await shell.OpenRepositoryAsync(FakeGitRepository());

    await shell.OpenRevisionRangeAsync("v1.0", "v2.0");

    Assert.Equal("v1.0", shell.FromRevision);
    Assert.Equal("v2.0", shell.ToRevision);
  }

  [Fact]
  public async Task OpeningAFilePair_EndsTheRepositorySession() {
    var shell = new ShellViewModel();
    await shell.OpenRepositoryAsync(FakeGitRepository());
    Assert.True(shell.IsRepositorySession);

    await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "a\n", "b\n"));

    Assert.False(shell.IsRepositorySession);
  }

  [Fact]
  public async Task History_WithoutARepository_IsEmpty() {
    var shell = new ShellViewModel();

    await shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "a\n", "b\n"));

    Assert.Empty(shell.History());
  }

  // A .git marker is enough for detection; the provider then shells out to git,
  // which reports a failure this test tolerates — the point is the session state.
  private string FakeGitRepository() {
    var repository = Path.Combine(_root, "repo");
    Directory.CreateDirectory(Path.Combine(repository, ".git"));
    return repository;
  }
}
