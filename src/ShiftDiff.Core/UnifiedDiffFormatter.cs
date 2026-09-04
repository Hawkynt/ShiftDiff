namespace ShiftDiff.Core;

public static class UnifiedDiffFormatter {
  public static IReadOnlyList<string> Format(UnifiedDiffFile file) {
    var lines = new List<string>();

    if (file.GitHeader is not null) {
      lines.AddRange(FormatGitHeader(file.GitHeader));
    }

    // A pure git rename/copy/mode-change block carries no "--- "/"+++ " pair —
    // mirrors UnifiedDiffParser.ParseFile's own fallback for that shape.
    if (file.GitHeader is null || file.Hunks.Count > 0) {
      lines.AddRange(FormatBody(file.Header, file.Hunks));
    }

    return lines;
  }

  private static IEnumerable<string> FormatBody(UnifiedDiffFileHeader header, IReadOnlyList<UnifiedDiffHunk> hunks) {
    yield return FormatFileHeaderLine("---", header.SourcePath, header.SourceRevision);
    yield return FormatFileHeaderLine("+++", header.TargetPath, header.TargetRevision);

    foreach (var hunk in hunks) {
      yield return FormatHunkHeader(hunk.Header);
      foreach (var line in hunk.Lines) {
        yield return FormatLine(line);
      }
    }
  }

  private const int SvnSeparatorLength = 67;

  public static IReadOnlyList<string> FormatSvn(UnifiedDiffFile file) {
    if (file.GitHeader is not null) {
      throw new NotSupportedException("SVN-style export does not support git extended headers.");
    }

    var lines = new List<string>
    {
            $"Index: {file.Header.SourcePath}",
            new string('=', SvnSeparatorLength),
        };
    lines.AddRange(FormatBody(file.Header, file.Hunks));

    return lines;
  }

  public static IReadOnlyList<string> FormatSvn(UnifiedDiffPatch patch) {
    var lines = new List<string>();
    foreach (var file in patch.Files) {
      lines.AddRange(FormatSvn(file));
    }

    return lines;
  }

  private static IEnumerable<string> FormatGitHeader(GitExtendedHeader header) {
    yield return $"diff --git a/{header.Header.OldPath} b/{header.Header.NewPath}";

    if (header.ModeChange is not null) {
      yield return $"old mode {header.ModeChange.OldMode}";
      yield return $"new mode {header.ModeChange.NewMode}";
    } else if (header.CreationMode is not null) {
      yield return header.CreationMode.Kind == GitFileCreationKind.NewFile
          ? $"new file mode {header.CreationMode.Mode}"
          : $"deleted file mode {header.CreationMode.Mode}";
    }

    if (header.SimilarityIndex is not null) {
      yield return header.SimilarityIndex.Kind == GitSimilarityKind.Similarity
          ? $"similarity index {header.SimilarityIndex.Percentage}%"
          : $"dissimilarity index {header.SimilarityIndex.Percentage}%";
    }

    if (header.RenameCopyMetadata is not null) {
      var metadata = header.RenameCopyMetadata;
      if (metadata.Kind == GitRenameCopyKind.Rename) {
        yield return $"rename from {metadata.SourcePath}";
        yield return $"rename to {metadata.TargetPath}";
      } else {
        yield return $"copy from {metadata.SourcePath}";
        yield return $"copy to {metadata.TargetPath}";
      }
    }

    if (header.IndexHash is not null) {
      yield return header.IndexHash.Mode is null
          ? $"index {header.IndexHash.OldHash}..{header.IndexHash.NewHash}"
          : $"index {header.IndexHash.OldHash}..{header.IndexHash.NewHash} {header.IndexHash.Mode}";
    }
  }

  public static IReadOnlyList<string> Format(UnifiedDiffPatch patch) {
    var lines = new List<string>();
    foreach (var file in patch.Files) {
      lines.AddRange(Format(file));
    }

    return lines;
  }

  private static string FormatFileHeaderLine(string marker, string path, string? revision) {
    return revision is null ? $"{marker} {path}" : $"{marker} {path}\t{revision}";
  }

  private static string FormatHunkHeader(UnifiedDiffHunkHeader header) {
    return $"@@ -{header.OldStart},{header.OldCount} +{header.NewStart},{header.NewCount} @@";
  }

  private static string FormatLine(UnifiedDiffLine line) {
    var marker = line.Kind switch {
      UnifiedDiffLineKind.Context => ' ',
      UnifiedDiffLineKind.Added => '+',
      UnifiedDiffLineKind.Removed => '-',
      _ => throw new ArgumentOutOfRangeException(nameof(line), line.Kind, "Unrecognized unified diff line kind."),
    };

    return $"{marker}{line.Content}";
  }
}
