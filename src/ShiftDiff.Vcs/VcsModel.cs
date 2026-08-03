namespace ShiftDiff.Vcs;

public enum VcsKind
{
    None,
    Git,
    Svn,
}

public enum VcsChangeKind
{
    Unchanged,
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    Untracked,
    Conflicted,
    Ignored,
}

// SPEC 14.2 File Role vocabulary, expressed for repository entries.
public sealed record VcsFileStatus(
    string Path,
    VcsChangeKind Kind,
    bool Staged = false,
    string? OriginalPath = null,
    int? SimilarityPercentage = null);

public sealed record VcsRevision(string Id, string Author, DateTimeOffset Timestamp, string Message);

public sealed record RepositoryInfo(VcsKind Kind, string Root);

// The working tree/copy is addressed with this sentinel so callers can ask for
// "the file as it currently sits on disk" through the same API as any revision.
public static class VcsRevisions
{
    public const string WorkingTree = "";
    public const string Head = "HEAD";
    public const string SvnBase = "BASE";
}
