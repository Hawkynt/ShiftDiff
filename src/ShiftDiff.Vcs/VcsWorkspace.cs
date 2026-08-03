using System.Text;

namespace ShiftDiff.Vcs;

public sealed record VcsFileComparison(VcsFileStatus Status, string OldPath, string NewPath, byte[] OldContent, byte[] NewContent);

// AC-006/AC-007: turns "a repository plus two revisions" into the plain
// old-bytes/new-bytes pairs the diff engine already understands, so neither the
// CLI nor the UI needs any VCS-specific knowledge to show a semantic diff.
public sealed class VcsWorkspace(IVcsProvider provider, string root)
{
    public IVcsProvider Provider { get; } = provider;

    public string Root { get; } = root;

    public IReadOnlyList<VcsFileStatus> ListChanges(
        string fromRevision = VcsRevisions.Head, string toRevision = VcsRevisions.WorkingTree) =>
        Provider.GetChanges(Root, fromRevision, toRevision)
            .Where(status => status.Kind is not (VcsChangeKind.Ignored or VcsChangeKind.Unchanged))
            .GroupBy(status => (status.Path, status.Kind))
            .Select(group => group.First())
            .ToList();

    public VcsFileComparison Load(
        VcsFileStatus status, string fromRevision = VcsRevisions.Head, string toRevision = VcsRevisions.WorkingTree)
    {
        var oldPath = status.OriginalPath ?? status.Path;
        var oldText = status.Kind == VcsChangeKind.Added || status.Kind == VcsChangeKind.Untracked
            ? string.Empty
            : Provider.GetFileContent(Root, oldPath, fromRevision);
        var newText = status.Kind == VcsChangeKind.Deleted
            ? string.Empty
            : Provider.GetFileContent(Root, status.Path, toRevision);

        return new VcsFileComparison(
            status,
            Describe(oldPath, fromRevision),
            Describe(status.Path, toRevision),
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText));
    }

    public static VcsWorkspace? Open(string path, IEnumerable<IVcsProvider>? providers = null)
    {
        var candidates = (providers ?? [new GitProvider(), new SvnProvider()]).ToArray();
        var info = RepositoryLocator.Detect(path, candidates);
        if (info is null) return null;

        var match = candidates.First(candidate => candidate.Kind == info.Kind);
        return new VcsWorkspace(match, info.Root);
    }

    private static string Describe(string path, string revision) =>
        string.IsNullOrEmpty(revision) ? path : $"{path}@{revision}";
}
