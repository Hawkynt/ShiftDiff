namespace ShiftDiff.Vcs;

// Pure parsing of git's stable plumbing formats — the part worth unit testing.
public static class GitOutputParser {
  // `git log --format` field separator: ASCII unit separator, chosen because it
  // cannot appear in an author name or commit subject.
  public const char LogFieldSeparator = '\u001f';

  /// <summary>Parses `git status --porcelain=v1` output.</summary>
  public static IReadOnlyList<VcsFileStatus> ParseStatus(string output) {
    var result = new List<VcsFileStatus>();
    foreach (var line in SplitLines(output)) {
      if (line.Length < 4) continue;

      var indexCode = line[0];
      var workTreeCode = line[1];
      var payload = line[3..];

      if (indexCode == '?' && workTreeCode == '?') {
        result.Add(new VcsFileStatus(Unquote(payload), VcsChangeKind.Untracked));
        continue;
      }

      if (indexCode == '!' && workTreeCode == '!') {
        result.Add(new VcsFileStatus(Unquote(payload), VcsChangeKind.Ignored));
        continue;
      }

      if (IsConflict(indexCode, workTreeCode)) {
        result.Add(new VcsFileStatus(Unquote(payload), VcsChangeKind.Conflicted));
        continue;
      }

      var (path, original) = SplitRenamePayload(payload);

      if (indexCode != ' ') {
        result.Add(new VcsFileStatus(path, ToChangeKind(indexCode), Staged: true, OriginalPath: original));
      }

      if (workTreeCode != ' ') {
        result.Add(new VcsFileStatus(path, ToChangeKind(workTreeCode), Staged: false, OriginalPath: original));
      }
    }

    return result;
  }

  /// <summary>Parses `git diff --name-status -M` output.</summary>
  public static IReadOnlyList<VcsFileStatus> ParseNameStatus(string output, bool staged = false) {
    var result = new List<VcsFileStatus>();
    foreach (var line in SplitLines(output)) {
      var fields = line.Split('\t');
      if (fields.Length < 2) continue;

      var code = fields[0];
      var kind = ToChangeKind(code[0]);
      int? similarity = code.Length > 1 && int.TryParse(code[1..], out var parsed) ? parsed : null;

      if (kind is VcsChangeKind.Renamed or VcsChangeKind.Copied && fields.Length >= 3) {
        result.Add(new VcsFileStatus(Unquote(fields[2]), kind, staged, Unquote(fields[1]), similarity));
        continue;
      }

      result.Add(new VcsFileStatus(Unquote(fields[1]), kind, staged, null, similarity));
    }

    return result;
  }

  /// <summary>Parses `git log` output using <see cref="LogFieldSeparator"/> between fields.</summary>
  public static IReadOnlyList<VcsRevision> ParseLog(string output) {
    var result = new List<VcsRevision>();
    foreach (var line in SplitLines(output)) {
      var fields = line.Split(LogFieldSeparator);
      if (fields.Length < 4) continue;
      if (!DateTimeOffset.TryParse(fields[2], out var timestamp)) timestamp = default;
      result.Add(new VcsRevision(fields[0], fields[1], timestamp, fields[3]));
    }

    return result;
  }

  private static bool IsConflict(char index, char workTree) =>
      index == 'U' || workTree == 'U'
      || (index == 'A' && workTree == 'A')
      || (index == 'D' && workTree == 'D');

  private static VcsChangeKind ToChangeKind(char code) => code switch {
    'A' => VcsChangeKind.Added,
    'M' => VcsChangeKind.Modified,
    'T' => VcsChangeKind.Modified,
    'D' => VcsChangeKind.Deleted,
    'R' => VcsChangeKind.Renamed,
    'C' => VcsChangeKind.Copied,
    'U' => VcsChangeKind.Conflicted,
    '?' => VcsChangeKind.Untracked,
    '!' => VcsChangeKind.Ignored,
    _ => VcsChangeKind.Unchanged,
  };

  private static (string Path, string? Original) SplitRenamePayload(string payload) {
    var separator = payload.IndexOf(" -> ", StringComparison.Ordinal);
    return separator < 0
        ? (Unquote(payload), null)
        : (Unquote(payload[(separator + 4)..]), Unquote(payload[..separator]));
  }

  // git quotes paths containing unusual bytes; strip the wrapping quotes so
  // callers get a usable relative path.
  private static string Unquote(string path) {
    var trimmed = path.Trim();
    return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
        ? trimmed[1..^1].Replace("\\\"", "\"")
        : trimmed;
  }

  private static IEnumerable<string> SplitLines(string output) =>
      output.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
