namespace ShiftDiff.Vcs;

// FR-031 SVN Support, via the svn CLI.
public sealed class SvnProvider(IProcessRunner? processRunner = null, string executable = "svn") : IVcsProvider
{
    private readonly IProcessRunner _processRunner = processRunner ?? SystemProcessRunner.Instance;

    public VcsKind Kind => VcsKind.Svn;

    public string Executable { get; } = executable;

    public RepositoryInfo? Detect(string path)
    {
        var directory = RepositoryLocator.DirectoryOf(path);
        if (directory is null) return null;

        var marker = RepositoryLocator.FindUpwards(directory, ".svn");
        return marker is null ? null : new RepositoryInfo(VcsKind.Svn, marker);
    }

    public IReadOnlyList<VcsFileStatus> GetWorkingChanges(string root) =>
        SvnOutputParser.ParseStatus(Run(root, "status", "--xml"));

    public IReadOnlyList<VcsFileStatus> GetChanges(string root, string fromRevision, string toRevision)
    {
        if (string.IsNullOrEmpty(fromRevision) && string.IsNullOrEmpty(toRevision)) return GetWorkingChanges(root);

        var range = string.IsNullOrEmpty(toRevision) ? fromRevision : $"{fromRevision}:{toRevision}";
        var output = Run(root, "diff", "--summarize", "--xml", "-r", range);
        return SvnOutputParser.ParseStatus(output);
    }

    public string GetFileContent(string root, string relativePath, string revision)
    {
        if (string.IsNullOrEmpty(revision))
        {
            var absolute = Path.Combine(root, relativePath);
            return File.Exists(absolute) ? File.ReadAllText(absolute) : string.Empty;
        }

        var result = _processRunner.Run(Executable, ["cat", "-r", revision, relativePath], root);
        return result.Succeeded ? result.StandardOutput : string.Empty;
    }

    public IReadOnlyList<VcsRevision> GetHistory(string root, string? relativePath = null, int limit = 50)
    {
        var arguments = new List<string> { "log", "--xml", "--limit", limit.ToString() };
        if (!string.IsNullOrEmpty(relativePath)) arguments.Add(relativePath);
        return SvnOutputParser.ParseLog(Run(root, arguments));
    }

    /// <summary>Raw `svn diff` output, which the unified diff parser can consume directly.</summary>
    public string GetUnifiedDiff(string root, string? revisionRange = null, string? relativePath = null)
    {
        var arguments = new List<string> { "diff" };
        if (!string.IsNullOrEmpty(revisionRange))
        {
            arguments.Add("-r");
            arguments.Add(revisionRange);
        }

        if (!string.IsNullOrEmpty(relativePath)) arguments.Add(relativePath);
        return Run(root, arguments);
    }

    private string Run(string root, params string[] arguments) => Run(root, (IReadOnlyList<string>)arguments);

    private string Run(string root, IReadOnlyList<string> arguments)
    {
        var result = _processRunner.Run(Executable, arguments, root);
        if (!result.Succeeded)
        {
            throw new VcsCommandException(
                $"svn {string.Join(' ', arguments)} failed ({result.ExitCode}): {result.StandardError.Trim()}");
        }

        return result.StandardOutput;
    }
}
