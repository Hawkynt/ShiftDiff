using ShiftDiff.Vcs;

namespace ShiftDiff.Vcs.Tests;

public class GitProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-vcs", Guid.NewGuid().ToString("N"));

    public GitProviderTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Detect_DirectoryContainingDotGit_ReturnsThatDirectoryAsRoot()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var nested = Directory.CreateDirectory(Path.Combine(_root, "src", "deep")).FullName;

        var info = new GitProvider(new FakeProcessRunner()).Detect(nested);

        Assert.NotNull(info);
        Assert.Equal(VcsKind.Git, info!.Kind);
        Assert.Equal(Path.GetFullPath(_root), Path.GetFullPath(info.Root));
    }

    [Fact]
    public void Detect_WorktreeWhereDotGitIsAFile_StillFindsTheRoot()
    {
        File.WriteAllText(Path.Combine(_root, ".git"), "gitdir: ../.git/worktrees/x\n");

        Assert.NotNull(new GitProvider(new FakeProcessRunner()).Detect(_root));
    }

    [Fact]
    public void Detect_PathWithoutRepository_ReturnsNull()
    {
        Assert.Null(new GitProvider(new FakeProcessRunner()).Detect(_root));
    }

    [Fact]
    public void Detect_NonExistentPath_ReturnsNull()
    {
        Assert.Null(new GitProvider(new FakeProcessRunner()).Detect(Path.Combine(_root, "nope")));
    }

    [Fact]
    public void GetWorkingChanges_AsksGitForPorcelainStatusAndParsesIt()
    {
        var runner = new FakeProcessRunner().Respond("status", " M src/File.cs\n?? new.txt\n");

        var changes = new GitProvider(runner).GetWorkingChanges(_root);

        Assert.Contains("--porcelain=v1", runner.LastCommandLine);
        Assert.Equal(2, changes.Count);
        Assert.Equal(VcsChangeKind.Modified, changes[0].Kind);
        Assert.Equal(VcsChangeKind.Untracked, changes[1].Kind);
    }

    [Fact]
    public void GetStagedChanges_UsesTheIndexDiffAndMarksEntriesStaged()
    {
        var runner = new FakeProcessRunner().Respond("--cached", "M\tsrc/File.cs\n");

        var change = Assert.Single(new GitProvider(runner).GetStagedChanges(_root));

        Assert.True(change.Staged);
        Assert.Contains("--cached", runner.LastCommandLine);
    }

    [Fact]
    public void GetChanges_BetweenTwoRevisions_PassesBothToGitDiff()
    {
        var runner = new FakeProcessRunner().Respond("diff", "M\tsrc/File.cs\n");

        new GitProvider(runner).GetChanges(_root, "v1.0", "v2.0");

        Assert.Contains("v1.0", runner.LastCommandLine);
        Assert.Contains("v2.0", runner.LastCommandLine);
        Assert.Contains("--name-status", runner.LastCommandLine);
    }

    [Fact]
    public void GetChanges_WithNoRevisions_FallsBackToTheWorkingTreeStatus()
    {
        var runner = new FakeProcessRunner().Respond("status", " M f.cs\n");

        var change = Assert.Single(new GitProvider(runner).GetChanges(_root, "", ""));

        Assert.Equal("f.cs", change.Path);
        Assert.Contains("status", runner.LastCommandLine);
    }

    [Fact]
    public void GetChanges_RevisionAgainstWorkingTree_OmitsTheSecondRevision()
    {
        var runner = new FakeProcessRunner().Respond("diff", "M\tf.cs\n");

        new GitProvider(runner).GetChanges(_root, "HEAD", "");

        Assert.Contains("HEAD", runner.LastCommandLine);
        Assert.DoesNotContain("HEAD HEAD", runner.LastCommandLine);
    }

    [Fact]
    public void GetFileContent_AtRevision_UsesGitShowWithRevisionColonPath()
    {
        var runner = new FakeProcessRunner().Respond("show", "file contents\n");

        var content = new GitProvider(runner).GetFileContent(_root, "src/File.cs", "HEAD");

        Assert.Equal("file contents\n", content);
        Assert.Contains("HEAD:src/File.cs", runner.LastCommandLine);
    }

    [Fact]
    public void GetFileContent_AtRevisionWhereFileDoesNotExist_ReturnsEmptyRatherThanThrowing()
    {
        var runner = new FakeProcessRunner().RespondWithFailure("show", "fatal: path does not exist");

        Assert.Equal(string.Empty, new GitProvider(runner).GetFileContent(_root, "gone.cs", "HEAD"));
    }

    [Fact]
    public void GetFileContent_WorkingTreeRevision_ReadsFromDisk()
    {
        File.WriteAllText(Path.Combine(_root, "onDisk.cs"), "live content");
        var runner = new FakeProcessRunner();

        var content = new GitProvider(runner).GetFileContent(_root, "onDisk.cs", VcsRevisions.WorkingTree);

        Assert.Equal("live content", content);
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public void GetFileContent_WorkingTreeFileMissing_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, new GitProvider(new FakeProcessRunner()).GetFileContent(_root, "nope.cs", ""));
    }

    [Fact]
    public void GetHistory_PassesTheLimitAndPathToGitLog()
    {
        var separator = GitOutputParser.LogFieldSeparator;
        var runner = new FakeProcessRunner().Respond("log", $"abc{separator}Ada{separator}2024-01-01T00:00:00Z{separator}Message\n");

        var revisions = new GitProvider(runner).GetHistory(_root, "src/File.cs", limit: 5);

        Assert.Single(revisions);
        Assert.Contains("--max-count=5", runner.LastCommandLine);
        Assert.Contains("src/File.cs", runner.LastCommandLine);
    }

    [Fact]
    public void FailingCommand_RaisesAVcsCommandExceptionCarryingGitsMessage()
    {
        var runner = new FakeProcessRunner().RespondWithFailure("status", "fatal: not a git repository", 128);

        var exception = Assert.Throws<VcsCommandException>(() => new GitProvider(runner).GetWorkingChanges(_root));

        Assert.Contains("not a git repository", exception.Message);
    }

    [Fact]
    public void Commands_RunInsideTheRepositoryRoot()
    {
        var runner = new FakeProcessRunner();

        new GitProvider(runner).GetWorkingChanges(_root);

        Assert.Equal(_root, runner.Invocations[0].WorkingDirectory);
    }
}
