namespace ShiftDiff.Vcs;

// FR-032 VCS Abstraction Layer. Git, SVN and the plain filesystem all answer
// the same questions; the UI and CLI never learn which one they are talking to.
public interface IVcsProvider {
  VcsKind Kind { get; }

  /// <summary>Walks up from <paramref name="path"/> looking for a repository root.</summary>
  RepositoryInfo? Detect(string path);

  /// <summary>Uncommitted changes in the working tree/copy.</summary>
  IReadOnlyList<VcsFileStatus> GetWorkingChanges(string root);

  /// <summary>Changes between two revisions; an empty revision means the working tree.</summary>
  IReadOnlyList<VcsFileStatus> GetChanges(string root, string fromRevision, string toRevision);

  /// <summary>Content of one file at one revision; an empty revision reads it from disk.</summary>
  string GetFileContent(string root, string relativePath, string revision);

  /// <summary>Commit/revision history for a path, newest first.</summary>
  IReadOnlyList<VcsRevision> GetHistory(string root, string? relativePath = null, int limit = 50);
}

public sealed class VcsCommandException(string message) : Exception(message);
