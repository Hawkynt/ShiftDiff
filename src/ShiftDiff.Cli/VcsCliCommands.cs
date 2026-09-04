using ShiftDiff.Core;
using ShiftDiff.Vcs;

namespace ShiftDiff.Cli;

// FR-030/FR-031/FR-033: `shiftdiff git ...` and `shiftdiff svn ...` open the
// repository through the VCS abstraction and render each changed file with the
// same semantic formatter the file comparison uses.
public static class VcsCliCommands {
  public static int Run(CliOptions options, TextWriter output, TextWriter error) =>
      Run(options, output, error, workspace: null);

  // The workspace seam keeps the command testable against a fake provider.
  public static int Run(CliOptions options, TextWriter output, TextWriter error, VcsWorkspace? workspace) {
    var kind = options.Command == CliCommand.Git ? VcsKind.Git : VcsKind.Svn;
    var operands = options.Operands;
    var verb = operands.Count > 0 ? operands[0] : "status";
    var revisionArguments = operands.Skip(1).Where(argument => !argument.StartsWith('-')).ToArray();
    var searchPath = Directory.Exists(verb) ? verb : Environment.CurrentDirectory;

    try {
      workspace ??= OpenWorkspace(kind, searchPath);
      if (workspace is null) {
        error.WriteLine($"error: no {kind.ToString().ToLowerInvariant()} repository found at {searchPath}");
        return ExitCode.InvalidInput;
      }

      return verb switch {
        "status" => RunStatus(workspace, options, output),
        "log" or "history" => RunHistory(workspace, output, revisionArguments),
        "diff" or "" => RunDiff(workspace, options, output, revisionArguments),
        _ => RunDiff(workspace, options, output, operands.Where(a => !a.StartsWith('-')).ToArray()),
      };
    } catch (VcsExecutableMissingException exception) {
      error.WriteLine($"error: {exception.Message}");
      return ExitCode.InvalidInput;
    } catch (VcsCommandException exception) {
      error.WriteLine($"error: {exception.Message}");
      return ExitCode.InvalidInput;
    }
  }

  private static VcsWorkspace? OpenWorkspace(VcsKind kind, string searchPath) {
    IVcsProvider provider = kind == VcsKind.Git ? new GitProvider() : new SvnProvider();
    var info = provider.Detect(searchPath);
    return info is null ? null : new VcsWorkspace(provider, info.Root);
  }

  private static int RunStatus(VcsWorkspace workspace, CliOptions options, TextWriter output) {
    var changes = workspace.Provider.GetWorkingChanges(workspace.Root);
    var interesting = changes
        .Where(change => change.Kind is not (VcsChangeKind.Ignored or VcsChangeKind.Unchanged))
        .ToArray();

    output.WriteLine($"# {workspace.Provider.Kind} repository at {workspace.Root}");
    foreach (var change in interesting) {
      var stage = change.Staged ? "staged  " : "worktree";
      var origin = change.OriginalPath is { } original ? $"  (from {original})" : string.Empty;
      output.WriteLine(
          $"{ChangeMarker.For(ToChangeType(change.Kind), options.UseEmoji)} {stage} {change.Kind.ToString().ToLowerInvariant(),-10} {change.Path}{origin}");
    }

    if (interesting.Length == 0) output.WriteLine("# working tree clean");

    return interesting.Length > 0 ? ExitCode.DifferencesFound : ExitCode.NoDifferences;
  }

  private static int RunHistory(VcsWorkspace workspace, TextWriter output, IReadOnlyList<string> arguments) {
    var path = arguments.Count > 0 ? arguments[0] : null;
    foreach (var revision in workspace.Provider.GetHistory(workspace.Root, path)) {
      output.WriteLine($"{revision.Id[..Math.Min(12, revision.Id.Length)]}  {revision.Timestamp:yyyy-MM-dd}  {revision.Author}  {revision.Message}");
    }

    return ExitCode.NoDifferences;
  }

  private static int RunDiff(
      VcsWorkspace workspace, CliOptions options, TextWriter output, IReadOnlyList<string> revisions) {
    var (from, to) = revisions.Count switch {
      0 => (workspace.Provider.Kind == VcsKind.Git ? VcsRevisions.Head : VcsRevisions.SvnBase, VcsRevisions.WorkingTree),
      1 => (revisions[0], VcsRevisions.WorkingTree),
      _ => (revisions[0], revisions[1]),
    };

    var changes = workspace.ListChanges(from, to);
    if (changes.Count == 0) {
      output.WriteLine("# no changes");
      return ExitCode.NoDifferences;
    }

    foreach (var status in changes) {
      var comparison = workspace.Load(status, from, to);
      var result = FileComparer.CompareSourceFiles(
          comparison.OldContent, comparison.NewContent, status.OriginalPath ?? status.Path, status.Path,
          options.IgnoreCase, options.Whitespace, options.Detection);

      if (options.Format == OutputFormat.Json) {
        output.WriteLine(JsonOutputFormatter.FormatComparison(
            comparison.OldPath, comparison.NewPath, result.Language, result.Comparison));
        continue;
      }

      foreach (var line in SemanticTextFormatter.Format(
                   comparison.OldPath, comparison.NewPath, result.Language, result.Comparison,
                   options.UseEmoji, options.ContextLines)) {
        output.WriteLine(line);
      }

      output.WriteLine();
    }

    return ExitCode.DifferencesFound;
  }

  private static ChangeType ToChangeType(VcsChangeKind kind) => kind switch {
    VcsChangeKind.Added or VcsChangeKind.Untracked => ChangeType.Added,
    VcsChangeKind.Deleted => ChangeType.Removed,
    VcsChangeKind.Modified => ChangeType.Edited,
    VcsChangeKind.Renamed => ChangeType.Moved,
    VcsChangeKind.Copied => ChangeType.Split,
    VcsChangeKind.Conflicted => ChangeType.Conflict,
    _ => ChangeType.Unchanged,
  };
}
