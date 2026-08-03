namespace ShiftDiff.Vcs;

// FR-030 Git Support. Shells out to the git CLI (the spec's recommended first
// backend); every command goes through IProcessRunner so the behaviour is
// testable without a repository.
public sealed class GitProvider(IProcessRunner? processRunner = null, string executable = "git") : IVcsProvider
{
    private readonly IProcessRunner _processRunner = processRunner ?? SystemProcessRunner.Instance;

    public VcsKind Kind => VcsKind.Git;

    public string Executable { get; } = executable;

    public RepositoryInfo? Detect(string path)
    {
        var directory = RepositoryLocator.DirectoryOf(path);
        if (directory is null) return null;

        var marker = RepositoryLocator.FindUpwards(directory, ".git");
        return marker is null ? null : new RepositoryInfo(VcsKind.Git, marker);
    }

    public IReadOnlyList<VcsFileStatus> GetWorkingChanges(string root) =>
        GitOutputParser.ParseStatus(Run(root, "status", "--porcelain=v1", "--untracked-files=normal"));

    public IReadOnlyList<VcsFileStatus> GetStagedChanges(string root) =>
        GitOutputParser.ParseNameStatus(Run(root, "diff", "--cached", "--name-status", "-M", "-C"), staged: true);

    public IReadOnlyList<VcsFileStatus> GetChanges(string root, string fromRevision, string toRevision)
    {
        if (string.IsNullOrEmpty(fromRevision) && string.IsNullOrEmpty(toRevision)) return GetWorkingChanges(root);

        var arguments = string.IsNullOrEmpty(toRevision)
            ? new[] { "diff", "--name-status", "-M", "-C", fromRevision }
            : ["diff", "--name-status", "-M", "-C", fromRevision, toRevision];

        var result = _processRunner.Run(Executable, arguments, root);
        if (result.Succeeded) return GitOutputParser.ParseNameStatus(result.StandardOutput);

        // A repository without commits has no HEAD to diff against; its working
        // tree is the whole change.
        if (string.IsNullOrEmpty(toRevision) && !RevisionExists(root, fromRevision)) return GetWorkingChanges(root);

        throw new VcsCommandException(
            $"git {string.Join(' ', arguments)} failed ({result.ExitCode}): {FirstLines(result.StandardError)}");
    }

    /// <summary>True when git can resolve the revision in that repository.</summary>
    public bool RevisionExists(string root, string revision) =>
        !string.IsNullOrEmpty(revision)
        && _processRunner.Run(Executable, ["rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}"], root).Succeeded;

    public string GetFileContent(string root, string relativePath, string revision)
    {
        if (string.IsNullOrEmpty(revision))
        {
            var absolute = Path.Combine(root, relativePath);
            return File.Exists(absolute) ? File.ReadAllText(absolute) : string.Empty;
        }

        var result = _processRunner.Run(Executable, ["show", $"{revision}:{relativePath}"], root);

        // A file that does not exist at that revision is an empty side of the
        // comparison (added/deleted), not an error.
        return result.Succeeded ? result.StandardOutput : string.Empty;
    }

    public IReadOnlyList<VcsRevision> GetHistory(string root, string? relativePath = null, int limit = 50)
    {
        var arguments = new List<string>
        {
            "log",
            $"--max-count={limit}",
            $"--format=%H%x1f%an%x1f%aI%x1f%s",
        };

        if (!string.IsNullOrEmpty(relativePath))
        {
            arguments.Add("--");
            arguments.Add(relativePath);
        }

        return GitOutputParser.ParseLog(Run(root, arguments));
    }

    /// <summary>Resolves the repository root git itself reports for a path.</summary>
    public string? GetRepositoryRoot(string path)
    {
        var directory = RepositoryLocator.DirectoryOf(path);
        if (directory is null) return null;

        var result = _processRunner.Run(Executable, ["rev-parse", "--show-toplevel"], directory);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    private string Run(string root, params string[] arguments) => Run(root, (IReadOnlyList<string>)arguments);

    private string Run(string root, IReadOnlyList<string> arguments)
    {
        var result = _processRunner.Run(Executable, arguments, root);
        if (!result.Succeeded)
        {
            throw new VcsCommandException(
                $"git {string.Join(' ', arguments)} failed ({result.ExitCode}): {FirstLines(result.StandardError)}");
        }

        return result.StandardOutput;
    }

    // Tools answer an invalid invocation with a page of usage text; only the
    // first lines say anything useful to the caller.
    private static string FirstLines(string text, int count = 3)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var kept = string.Join(" / ", lines.Take(count)).Trim();
        return lines.Length > count ? kept + " …" : kept;
    }
}
