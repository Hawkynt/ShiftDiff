namespace ShiftDiff.Vcs;

// FR-032: repository detection shared by the providers, and the entry point the
// UI/CLI use when the user simply drops a folder on the application.
public static class RepositoryLocator {
  public static string? DirectoryOf(string path) {
    if (Directory.Exists(path)) return Path.GetFullPath(path);
    if (File.Exists(path)) return Path.GetDirectoryName(Path.GetFullPath(path));
    return null;
  }

  /// <summary>Walks up from <paramref name="startDirectory"/> until a directory contains <paramref name="markerName"/>.</summary>
  public static string? FindUpwards(string startDirectory, string markerName) {
    var current = new DirectoryInfo(startDirectory);
    while (current is not null) {
      var markerPath = Path.Combine(current.FullName, markerName);
      if (Directory.Exists(markerPath) || File.Exists(markerPath)) return current.FullName;
      current = current.Parent;
    }

    return null;
  }

  /// <summary>Finds the closest repository of any supported kind, preferring the deepest match.</summary>
  public static RepositoryInfo? Detect(string path, IEnumerable<IVcsProvider>? providers = null) {
    providers ??= [new GitProvider(), new SvnProvider()];
    RepositoryInfo? best = null;
    foreach (var provider in providers) {
      if (provider.Detect(path) is not { } info) continue;
      if (best is null || info.Root.Length > best.Root.Length) best = info;
    }

    return best;
  }
}
