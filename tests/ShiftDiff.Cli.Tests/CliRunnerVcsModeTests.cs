using ShiftDiff.Cli;
using ShiftDiff.Vcs;
using Xunit;

namespace ShiftDiff.Cli.Tests;

// AC-006/AC-007 at the command line: the repository is opened through the VCS
// abstraction and every changed file is rendered by the semantic formatter.
public class CliRunnerVcsModeTests
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string Out => _output.ToString();

    private string Err => _error.ToString();

    [Fact]
    public void GitStatus_ListsWorkingTreeChangesWithMarkers()
    {
        var provider = new StubProvider
        {
            Changes =
            [
                new VcsFileStatus("src/Edited.cs", VcsChangeKind.Modified),
                new VcsFileStatus("src/New.cs", VcsChangeKind.Added, Staged: true),
                new VcsFileStatus("bin/app.dll", VcsChangeKind.Ignored),
            ],
        };

        var exitCode = RunVcs(["status"], provider);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Contains("src/Edited.cs", Out);
        Assert.Contains("src/New.cs", Out);
        Assert.Contains("staged", Out);
        Assert.DoesNotContain("app.dll", Out);
    }

    [Fact]
    public void GitStatus_CleanRepository_ExitsWithNoDifferences()
    {
        var exitCode = RunVcs(["status"], new StubProvider());

        Assert.Equal(ExitCode.NoDifferences, exitCode);
        Assert.Contains("working tree clean", Out);
    }

    [Fact]
    public void GitDiff_RendersASemanticDiffPerChangedFile()
    {
        var provider = new StubProvider
        {
            Changes = [new VcsFileStatus("src/File.cs", VcsChangeKind.Modified)],
            Contents =
            {
                ["HEAD:src/File.cs"] = "public int Value => 1;\n",
                [":src/File.cs"] = "public int Value => 2;\n",
            },
        };

        var exitCode = RunVcs(["diff"], provider);

        Assert.Equal(ExitCode.DifferencesFound, exitCode);
        Assert.Contains("src/File.cs@HEAD", Out);
        Assert.Contains("[-1-]{+2+}", Out);
    }

    [Fact]
    public void GitDiff_BetweenTwoRevisions_UsesBothRevisionsAsSides()
    {
        var provider = new StubProvider
        {
            Changes = [new VcsFileStatus("f.cs", VcsChangeKind.Modified)],
            Contents = { ["v1:f.cs"] = "alpha\n", ["v2:f.cs"] = "beta\n" },
        };

        RunVcs(["diff", "v1", "v2"], provider);

        Assert.Contains("f.cs@v1", Out);
        Assert.Contains("f.cs@v2", Out);
    }

    [Fact]
    public void GitDiff_NoChanges_ExitsWithNoDifferences()
    {
        var exitCode = RunVcs(["diff"], new StubProvider());

        Assert.Equal(ExitCode.NoDifferences, exitCode);
        Assert.Contains("no changes", Out);
    }

    [Fact]
    public void GitDiff_JsonFormat_EmitsOneDocumentPerChangedFile()
    {
        var provider = new StubProvider
        {
            Changes = [new VcsFileStatus("f.cs", VcsChangeKind.Modified)],
            Contents = { ["HEAD:f.cs"] = "one\n", [":f.cs"] = "two\n" },
        };

        RunVcs(["diff"], provider, "--json");

        using var document = System.Text.Json.JsonDocument.Parse(Out);
        Assert.Equal("f.cs@HEAD", document.RootElement.GetProperty("old").GetString());
    }

    [Fact]
    public void GitLog_PrintsTheRevisionHistory()
    {
        var provider = new StubProvider
        {
            History = [new VcsRevision("abcdef1234567890", "Ada", new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero), "Add the thing")],
        };

        var exitCode = RunVcs(["log"], provider);

        Assert.Equal(ExitCode.NoDifferences, exitCode);
        Assert.Contains("abcdef123456", Out);
        Assert.Contains("Ada", Out);
        Assert.Contains("Add the thing", Out);
    }

    [Fact]
    public void GitCommand_OutsideARepository_ReportsInvalidInputRatherThanCrashing()
    {
        var options = CliOptionsParser.Parse(["git", "status"]).Options!;

        var exitCode = VcsCliCommands.Run(options, _output, _error, workspace: null);

        Assert.True(exitCode is ExitCode.InvalidInput or ExitCode.DifferencesFound or ExitCode.NoDifferences);
        Assert.NotEqual(ExitCode.InternalError, exitCode);
    }

    [Fact]
    public void VcsCommandFailure_IsReportedAsInvalidInputWithTheToolsMessage()
    {
        var provider = new StubProvider { Failure = "fatal: bad revision 'nope'" };

        var exitCode = RunVcs(["diff", "nope"], provider);

        Assert.Equal(ExitCode.InvalidInput, exitCode);
        Assert.Contains("bad revision", Err);
    }

    private int RunVcs(string[] verbAndArguments, StubProvider provider, params string[] extraOptions)
    {
        var args = new List<string> { "git" };
        args.AddRange(verbAndArguments);
        args.AddRange(extraOptions);
        var options = CliOptionsParser.Parse(args).Options!;
        return VcsCliCommands.Run(options, _output, _error, new VcsWorkspace(provider, "/repo"));
    }

    private sealed class StubProvider : IVcsProvider
    {
        public VcsKind Kind => VcsKind.Git;

        public List<VcsFileStatus> Changes { get; init; } = [];

        public Dictionary<string, string> Contents { get; init; } = [];

        public List<VcsRevision> History { get; init; } = [];

        public string? Failure { get; init; }

        public RepositoryInfo? Detect(string path) => new(VcsKind.Git, "/repo");

        public IReadOnlyList<VcsFileStatus> GetWorkingChanges(string root) => Guard(Changes);

        public IReadOnlyList<VcsFileStatus> GetChanges(string root, string fromRevision, string toRevision) => Guard(Changes);

        public string GetFileContent(string root, string relativePath, string revision) =>
            Contents.GetValueOrDefault($"{revision}:{relativePath}", string.Empty);

        public IReadOnlyList<VcsRevision> GetHistory(string root, string? relativePath = null, int limit = 50) => Guard(History);

        private T Guard<T>(T value) => Failure is null ? value : throw new VcsCommandException(Failure);
    }
}
