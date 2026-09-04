using ShiftDiff.Core;

namespace ShiftDiff.Cli;

public static class CliRunner {
  public static int Run(string[] args, TextWriter output, TextWriter error) {
    var parsed = CliOptionsParser.Parse(args);
    if (parsed.Options is not { } options) {
      error.WriteLine($"error: {parsed.Error}");
      WriteUsage(error);
      return ExitCode.InvalidInput;
    }

    try {
      return Dispatch(options, output, error);
    } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or FileNotFoundException) {
      error.WriteLine($"error: {exception.Message}");
      return ExitCode.InvalidInput;
    } catch (Exception exception) {
      error.WriteLine($"internal error: {exception.Message}");
      return ExitCode.InternalError;
    }
  }

  private static int Dispatch(CliOptions options, TextWriter output, TextWriter error) => options.Command switch {
    CliCommand.Help => WriteUsage(output),
    CliCommand.Version => WriteVersion(output),
    CliCommand.Compare => RunCompare(options, output, error),
    CliCommand.Compare3 => RunCompare3(options, output, error),
    CliCommand.Compare4 => RunCompare4(options, output, error),
    CliCommand.ApplyPatch => RunApplyPatch(options, output, error),
    CliCommand.ExportPatch => RunExportPatch(options, output, error),
    CliCommand.Git or CliCommand.Svn => VcsCliCommands.Run(options, output, error),
    _ => WriteUsage(output),
  };

  private static int RunCompare(CliOptions options, TextWriter output, TextWriter error) {
    var oldPath = options.Operands[0];
    var newPath = options.Operands[1];

    if (Directory.Exists(oldPath) && Directory.Exists(newPath)) {
      return RunFolderCompare(options, oldPath, newPath, output);
    }

    if (Directory.Exists(oldPath) || Directory.Exists(newPath)) {
      error.WriteLine("error: compare needs either two files or two folders");
      return ExitCode.InvalidInput;
    }

    var oldContent = File.ReadAllBytes(oldPath);
    var newContent = File.ReadAllBytes(newPath);

    if (BinaryFileDetector.IsBinary(oldContent) || BinaryFileDetector.IsBinary(newContent)) {
      var equal = BinaryFileDetector.AreEqual(oldContent, newContent);
      output.WriteLine(equal
          ? $"Binary files {oldPath} and {newPath} are identical"
          : $"Binary files {oldPath} and {newPath} differ");
      return equal ? ExitCode.NoDifferences : ExitCode.DifferencesFound;
    }

    if (options.Format is not (OutputFormat.Unified or OutputFormat.Git or OutputFormat.Svn)
        && StructuredCompare(options, oldPath, newPath, oldContent, newContent, output) is { } structuredExit) {
      return structuredExit;
    }

    var result = FileComparer.CompareSourceFiles(
        oldContent, newContent, oldPath, newPath, options.IgnoreCase, options.Whitespace, options.Detection);

    var hasDifferences = result.Comparison.Changes.Any(change => change.ChangeType != ChangeType.Unchanged);

    switch (options.Format) {
      case OutputFormat.Json:
        output.WriteLine(JsonOutputFormatter.FormatComparison(oldPath, newPath, result.Language, result.Comparison));
        break;
      case OutputFormat.Unified:
      case OutputFormat.Git:
        WriteLines(output, UnifiedDiffFormatter.Format(BuildPatch(options, result, oldPath, newPath)));
        break;
      case OutputFormat.Svn:
        WriteLines(output, UnifiedDiffFormatter.FormatSvn(BuildPatch(options, result, oldPath, newPath)));
        break;
      default:
        WriteLines(output, SemanticTextFormatter.Format(
            oldPath, newPath, result.Language, result.Comparison, options.UseEmoji, options.ContextLines));
        break;
    }

    return hasDifferences ? ExitCode.DifferencesFound : ExitCode.NoDifferences;
  }

  private static UnifiedDiffFile BuildPatch(
      CliOptions options, SourceFileComparisonResult result, string oldPath, string newPath) =>
      BuildPatch(options, result.Comparison.Changes, oldPath, newPath);

  private static UnifiedDiffFile BuildPatch(
      CliOptions options, IReadOnlyList<LineChange> changes, string oldPath, string newPath) =>
      options.Format == OutputFormat.Git
          ? UnifiedDiffBuilder.BuildGit(changes, oldPath, newPath, options.ContextLines)
          : UnifiedDiffBuilder.Build(changes, oldPath, newPath, options.ContextLines);

  private static int RunFolderCompare(CliOptions options, string basePath, string targetPath, TextWriter output) {
    var baseFiles = ReadFolder(basePath);
    var targetFiles = ReadFolder(targetPath);

    var changes = FolderComparer.Compare(baseFiles, targetFiles);
    changes = FolderMoveDetector.Detect(changes, baseFiles, targetFiles);
    changes = FolderCopyDetector.Detect(changes, targetFiles);
    changes = FolderRenameDetector.Detect(changes, baseFiles, targetFiles);

    if (options.Format == OutputFormat.Json) {
      output.WriteLine(JsonOutputFormatter.FormatFolderComparison(basePath, targetPath, changes));
    } else {
      WriteLines(output, FolderChangeTextFormatter.Format(basePath, targetPath, changes, options.UseEmoji));
    }

    return changes.Any(change => change.ChangeType != FolderChangeType.Unchanged)
        ? ExitCode.DifferencesFound
        : ExitCode.NoDifferences;
  }

  private static Dictionary<string, byte[]> ReadFolder(string root) {
    var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
      var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
      files[relative] = File.ReadAllBytes(path);
    }

    return files;
  }

  private static int? StructuredCompare(
      CliOptions options, string oldPath, string newPath, byte[] oldContent, byte[] newContent, TextWriter output) {
    if (HasMatchingExtension(oldPath, newPath, ".ini")) {
      var changes = IniComparer.Compare(oldContent, newContent);
      WriteLines(output, IniChangeFormatter.Format(changes));
      return Exit(changes.Any(change => change.ChangeType != IniChangeType.Unchanged));
    }

    if (HasMatchingExtension(oldPath, newPath, ".json")) {
      var changes = JsonComparer.Compare(oldContent, newContent);
      WriteLines(output, JsonChangeFormatter.Format(changes));
      return Exit(changes.Any(change => change.ChangeType != JsonChangeType.Unchanged));
    }

    if (HasMatchingExtension(oldPath, newPath, ".xml")) {
      var changes = XmlComparer.Compare(oldContent, newContent);
      WriteLines(output, XmlChangeFormatter.Format(changes));
      return Exit(changes.Any(change => change.ChangeType != XmlChangeType.Unchanged));
    }

    if (HasMatchingExtension(oldPath, newPath, ".md")) {
      var changes = MarkdownMoveDetector.Detect(MarkdownComparer.Compare(oldContent, newContent));
      WriteLines(output, MarkdownChangeFormatter.Format(changes));
      return Exit(changes.Any(change => change.ChangeType != MarkdownChangeType.Unchanged));
    }

    return null;

    static int Exit(bool different) => different ? ExitCode.DifferencesFound : ExitCode.NoDifferences;
  }

  private static int RunCompare3(CliOptions options, TextWriter output, TextWriter error) {
    var baseLines = File.ReadAllLines(options.Operands[0]);
    var localLines = File.ReadAllLines(options.Operands[1]);
    var remoteLines = File.ReadAllLines(options.Operands[2]);

    var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines, options.IgnoreCase, options.Whitespace);
    var merge = ThreeWayMerger.Merge(changes);
    var conflicts = merge.Conflicts.Length;

    if (options.Format == OutputFormat.Json) {
      output.WriteLine(JsonOutputFormatter.FormatThreeWay(changes, MergedWithMarkers(changes)));
    } else {
      WriteLines(output, MergedWithMarkers(changes));
    }

    if (conflicts > 0) {
      error.WriteLine($"{conflicts} conflict(s) require resolution");
      return ExitCode.Conflicts;
    }

    return changes.Any(change => change.ChangeType != ChangeType.Unchanged)
        ? ExitCode.DifferencesFound
        : ExitCode.NoDifferences;
  }

  private static int RunCompare4(CliOptions options, TextWriter output, TextWriter error) {
    var baseLines = File.ReadAllLines(options.Operands[0]);
    var localLines = File.ReadAllLines(options.Operands[1]);
    var remoteLines = File.ReadAllLines(options.Operands[2]);
    var targetLines = File.ReadAllLines(options.Operands[3]);

    var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines, options.IgnoreCase, options.Whitespace);
    var merge = ThreeWayMerger.Merge(changes);
    var validation = FourWayValidator.Validate(merge.Lines, targetLines);

    if (options.Format == OutputFormat.Json) {
      var comparison = new FileComparisonResult(validation.Discrepancies, []);
      output.WriteLine(JsonOutputFormatter.FormatComparison(
          options.Operands[2], options.Operands[3], SourceLanguage.PlainText, comparison));
    } else {
      output.WriteLine($"--- merged({options.Operands[0]}, {options.Operands[1]}, {options.Operands[2]})");
      output.WriteLine($"+++ {options.Operands[3]}");
      output.WriteLine(validation.Matches
          ? "# target matches the reconstructed merge result"
          : $"# target differs from the reconstructed merge result in {validation.Discrepancies.Length} line(s)");
      WriteLines(output, SemanticTextFormatter
          .Format(options.Operands[0], options.Operands[3], SourceLanguage.PlainText,
              new FileComparisonResult(validation.Discrepancies, []), options.UseEmoji, options.ContextLines)
          .Skip(3));
    }

    if (merge.Conflicts.Length > 0) {
      error.WriteLine($"{merge.Conflicts.Length} conflict(s) require resolution");
      return ExitCode.Conflicts;
    }

    return validation.Matches ? ExitCode.NoDifferences : ExitCode.DifferencesFound;
  }

  private static IReadOnlyList<string> MergedWithMarkers(IReadOnlyList<ThreeWayChange> changes) {
    var lines = new List<string>();
    var i = 0;
    while (i < changes.Count) {
      if (changes[i].ChangeType != ChangeType.Conflict) {
        AppendResolvedLine(changes[i], lines);
        i++;
        continue;
      }

      var localBlock = new List<string>();
      var remoteBlock = new List<string>();
      while (i < changes.Count && changes[i].ChangeType == ChangeType.Conflict) {
        if (changes[i].LocalLine is { } local) localBlock.Add(local);
        if (changes[i].RemoteLine is { } remote) remoteBlock.Add(remote);
        i++;
      }

      lines.Add("<<<<<<< local");
      lines.AddRange(localBlock);
      lines.Add("=======");
      lines.AddRange(remoteBlock);
      lines.Add(">>>>>>> remote");
    }

    return lines;
  }

  private static int RunApplyPatch(CliOptions options, TextWriter output, TextWriter error) {
    var sourcePath = options.Operands[0];
    var patchPath = options.Operands[1];
    var sourceLines = File.ReadAllLines(sourcePath);
    var patch = UnifiedDiffParser.ParsePatch(File.ReadAllLines(patchPath));

    if (patch.Files.Count == 0) {
      error.WriteLine("error: patch contains no file entries");
      return ExitCode.InvalidInput;
    }

    IReadOnlyList<string> resultLines;
    try {
      switch (options.PatchMode) {
        case PatchApplyMode.Fuzzy: {
            var applied = PatchApplier.ApplyFileFuzzy(sourceLines, patch.Files[0]);
            resultLines = applied.Lines;
            error.WriteLine($"# applied fuzzily ({Describe(applied.Confidence)})");
            break;
          }

        case PatchApplyMode.Semantic: {
            var applied = PatchApplier.ApplyFileSemantic(sourceLines, patch.Files[0]);
            resultLines = applied.Lines;
            error.WriteLine($"# applied semantically ({Describe(applied.Confidence)})");
            break;
          }

        default:
          resultLines = PatchApplier.ApplyFileExact(sourceLines, patch.Files[0]);
          break;
      }
    } catch (PatchApplicationException exception) {
      error.WriteLine($"error: {exception.Message}");
      return ExitCode.Conflicts;
    }

    if (options.OutPath is { } outPath) {
      if (File.Exists(outPath) && !options.Force) {
        error.WriteLine($"error: {outPath} already exists (pass --force to overwrite)");
        return ExitCode.InvalidInput;
      }

      File.WriteAllLines(outPath, resultLines);
      output.WriteLine($"# wrote {resultLines.Count} line(s) to {outPath}");
    } else {
      WriteLines(output, resultLines);
    }

    return sourceLines.SequenceEqual(resultLines) ? ExitCode.NoDifferences : ExitCode.DifferencesFound;
  }

  // AC-005: a fuzzy/semantic placement still counts as a successful reconstruction;
  // only an application failure (PatchApplicationException) is reported as a conflict.
  private static string Describe(PatchApplicationConfidence confidence) => confidence switch {
    PatchApplicationConfidence.Exact => "exact position",
    PatchApplicationConfidence.High => "high confidence, shifted position",
    _ => "relocated block",
  };

  private static int RunExportPatch(CliOptions options, TextWriter output, TextWriter error) {
    var oldPath = options.Operands[0];
    var newPath = options.Operands[1];
    var result = FileComparer.CompareSourceFiles(
        File.ReadAllBytes(oldPath), File.ReadAllBytes(newPath), oldPath, newPath,
        options.IgnoreCase, options.Whitespace, options.Detection);

    var file = BuildPatch(options, result.Comparison.Changes, oldPath, newPath);
    var lines = options.Format == OutputFormat.Svn
        ? UnifiedDiffFormatter.FormatSvn(file)
        : UnifiedDiffFormatter.Format(file);

    if (options.OutPath is { } outPath) {
      if (File.Exists(outPath) && !options.Force) {
        error.WriteLine($"error: {outPath} already exists (pass --force to overwrite)");
        return ExitCode.InvalidInput;
      }

      File.WriteAllLines(outPath, lines);
      output.WriteLine($"# wrote patch to {outPath}");
    } else {
      WriteLines(output, lines);
    }

    return file.Hunks.Count > 0 ? ExitCode.DifferencesFound : ExitCode.NoDifferences;
  }

  private static void AppendResolvedLine(ThreeWayChange change, List<string> lines) {
    switch (change.ChangeType) {
      case ChangeType.Removed:
        break;
      case ChangeType.Unchanged:
        lines.Add(change.BaseLine!);
        break;
      default:
        lines.Add(change.LocalLine ?? change.RemoteLine!);
        break;
    }
  }

  private static void WriteLines(TextWriter output, IEnumerable<string> lines) {
    foreach (var line in lines) output.WriteLine(line);
  }

  private static bool HasMatchingExtension(string oldPath, string newPath, string extension) =>
      string.Equals(Path.GetExtension(oldPath), extension, StringComparison.OrdinalIgnoreCase) &&
      string.Equals(Path.GetExtension(newPath), extension, StringComparison.OrdinalIgnoreCase);

  private static int WriteVersion(TextWriter output) {
    var version = typeof(CliRunner).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    output.WriteLine($"shiftdiff {version}");
    return ExitCode.NoDifferences;
  }

  private static int WriteUsage(TextWriter writer) {
    writer.WriteLine("shiftdiff — semantic diff viewer");
    writer.WriteLine();
    writer.WriteLine("usage:");
    writer.WriteLine("  shiftdiff compare <old> <new>                      compare two files or two folders");
    writer.WriteLine("  shiftdiff compare3 <base> <local> <remote>         three-way merge preview");
    writer.WriteLine("  shiftdiff compare4 <base> <local> <remote> <target> validate a reconstructed target");
    writer.WriteLine("  shiftdiff apply-patch <source> <patch> [--out f]   reconstruct a target from a patch");
    writer.WriteLine("  shiftdiff export-patch <old> <new> [--out f]       write a unified/svn patch");
    writer.WriteLine("  shiftdiff git <status|diff|show> [args]            compare against a git repository");
    writer.WriteLine("  shiftdiff svn <status|diff> [args]                 compare against an svn working copy");
    writer.WriteLine();
    writer.WriteLine("options:");
    writer.WriteLine("  --format <semantic|unified|git|svn|json>  output format (default: semantic)");
    writer.WriteLine("  --json                                    shorthand for --format json");
    writer.WriteLine("  --mode <strict|balanced|aggressive>       semantic detection aggressiveness");
    writer.WriteLine("  --patch-mode <exact|fuzzy|semantic>       patch application strategy");
    writer.WriteLine("  --ignore-case                             compare case-insensitively");
    writer.WriteLine("  --ignore-whitespace <none|trim|normalize|removeall>");
    writer.WriteLine("  --context <n>                             context lines around changes (default: 3)");
    writer.WriteLine("  --emoji / --no-emoji                      change markers as emoji or plain text");
    writer.WriteLine("  --out <file>                              write result to a file");
    writer.WriteLine("  --force                                   allow overwriting an existing --out file");
    writer.WriteLine();
    writer.WriteLine("exit codes: 0 no differences · 1 differences · 2 conflicts · 3 invalid input · 4 internal error");
    return ExitCode.NoDifferences;
  }
}
