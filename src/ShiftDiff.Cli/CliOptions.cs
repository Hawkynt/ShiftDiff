using ShiftDiff.Core;

namespace ShiftDiff.Cli;

public enum CliCommand {
  Help,
  Version,
  Compare,
  Compare3,
  Compare4,
  ApplyPatch,
  ExportPatch,
  Git,
  Svn,
}

public enum OutputFormat {
  Semantic,
  Unified,
  Git,
  Svn,
  Json,
}

public enum PatchApplyMode {
  Exact,
  Fuzzy,
  Semantic,
}

public sealed record CliOptions(
    CliCommand Command,
    IReadOnlyList<string> Operands,
    OutputFormat Format = OutputFormat.Semantic,
    bool IgnoreCase = false,
    WhitespaceMode Whitespace = WhitespaceMode.None,
    DetectionMode Detection = DetectionMode.Balanced,
    string? OutPath = null,
    PatchApplyMode PatchMode = PatchApplyMode.Exact,
    bool UseEmoji = false,
    bool Force = false,
    int ContextLines = 3);

public sealed record CliParseResult(CliOptions? Options, string? Error) {
  public static CliParseResult Ok(CliOptions options) => new(options, null);

  public static CliParseResult Fail(string error) => new(null, error);

  public bool IsValid => Options is not null;
}

// SPEC section 12 command surface. Legacy positional invocations
// (`shiftdiff old new`, `shiftdiff base local remote`, `--patch p --source s`)
// stay supported so existing difftool configurations keep working.
public static class CliOptionsParser {
  public static CliParseResult Parse(IReadOnlyList<string> args) {
    ArgumentNullException.ThrowIfNull(args);
    if (args.Count == 0) return CliParseResult.Ok(new CliOptions(CliCommand.Help, []));

    var command = ReadCommand(args[0]);
    var rest = command is null ? args : args.Skip(1).ToArray();

    // `git`/`svn` forward their own verbs and flags to the provider, so an
    // unrecognized option there is passed through instead of rejected.
    var passThroughUnknownOptions = command is CliCommand.Git or CliCommand.Svn;
    var operands = new List<string>();
    var options = new CliOptions(command ?? CliCommand.Compare, operands);
    string? patchPath = null;
    string? sourcePath = null;

    for (var i = 0; i < rest.Count; i++) {
      var argument = rest[i];
      switch (argument) {
        case "--help" or "-h":
          return CliParseResult.Ok(options with { Command = CliCommand.Help });
        case "--version":
          return CliParseResult.Ok(options with { Command = CliCommand.Version });
        case "--json":
          options = options with { Format = OutputFormat.Json };
          continue;
        case "--ignore-case":
          options = options with { IgnoreCase = true };
          continue;
        case "--emoji":
          options = options with { UseEmoji = true };
          continue;
        case "--no-emoji":
          options = options with { UseEmoji = false };
          continue;
        case "--force":
          options = options with { Force = true };
          continue;
      }

      if (TryReadValue(rest, ref i, "--format", out var formatText)) {
        if (!TryParseFormat(formatText, out var format)) return CliParseResult.Fail($"unknown output format '{formatText}'");
        options = options with { Format = format };
        continue;
      }

      if (TryReadValue(rest, ref i, "--ignore-whitespace", out var whitespaceText)) {
        if (!Enum.TryParse<WhitespaceMode>(whitespaceText, ignoreCase: true, out var whitespace)) return CliParseResult.Fail($"unknown whitespace mode '{whitespaceText}'");
        options = options with { Whitespace = whitespace };
        continue;
      }

      if (TryReadValue(rest, ref i, "--mode", out var modeText)) {
        if (!Enum.TryParse<DetectionMode>(modeText, ignoreCase: true, out var detection)) return CliParseResult.Fail($"unknown detection mode '{modeText}'");
        options = options with { Detection = detection };
        continue;
      }

      if (TryReadValue(rest, ref i, "--patch-mode", out var patchModeText)) {
        if (!Enum.TryParse<PatchApplyMode>(patchModeText, ignoreCase: true, out var patchMode)) return CliParseResult.Fail($"unknown patch mode '{patchModeText}'");
        options = options with { PatchMode = patchMode };
        continue;
      }

      if (TryReadValue(rest, ref i, "--context", out var contextText)) {
        if (!int.TryParse(contextText, out var context) || context < 0) return CliParseResult.Fail($"invalid context line count '{contextText}'");
        options = options with { ContextLines = context };
        continue;
      }

      if (TryReadValue(rest, ref i, "--out", out var outPath)) {
        options = options with { OutPath = outPath };
        continue;
      }

      if (TryReadValue(rest, ref i, "--patch", out var legacyPatch)) {
        patchPath = legacyPatch;
        continue;
      }

      if (TryReadValue(rest, ref i, "--source", out var legacySource)) {
        sourcePath = legacySource;
        continue;
      }

      if (argument.StartsWith('-') && argument.Length > 1 && !passThroughUnknownOptions) {
        return CliParseResult.Fail($"unknown option '{argument}'");
      }

      operands.Add(argument);
    }

    if (patchPath is not null || sourcePath is not null) {
      if (patchPath is null || sourcePath is null) return CliParseResult.Fail("--patch requires --source");
      return CliParseResult.Ok(options with { Command = CliCommand.ApplyPatch, Operands = [sourcePath, patchPath] });
    }

    if (command is null) {
      var inferred = operands.Count switch {
        2 => CliCommand.Compare,
        3 => CliCommand.Compare3,
        4 => CliCommand.Compare4,
        _ => (CliCommand?)null,
      };

      if (inferred is null) return CliParseResult.Fail($"expected 2 to 4 files, got {operands.Count}");
      options = options with { Command = inferred.Value };
    }

    var expected = options.Command switch {
      CliCommand.Compare => 2,
      CliCommand.Compare3 => 3,
      CliCommand.Compare4 => 4,
      CliCommand.ApplyPatch => 2,
      CliCommand.ExportPatch => 2,
      _ => -1,
    };

    if (expected >= 0 && operands.Count != expected) {
      return CliParseResult.Fail($"{Name(options.Command)} expects {expected} file argument(s), got {operands.Count}");
    }

    return CliParseResult.Ok(options);
  }

  public static string Name(CliCommand command) => command switch {
    CliCommand.Compare => "compare",
    CliCommand.Compare3 => "compare3",
    CliCommand.Compare4 => "compare4",
    CliCommand.ApplyPatch => "apply-patch",
    CliCommand.ExportPatch => "export-patch",
    CliCommand.Git => "git",
    CliCommand.Svn => "svn",
    CliCommand.Version => "version",
    _ => "help",
  };

  private static CliCommand? ReadCommand(string argument) => argument switch {
    "compare" => CliCommand.Compare,
    "compare3" => CliCommand.Compare3,
    "compare4" => CliCommand.Compare4,
    "apply-patch" => CliCommand.ApplyPatch,
    "export-patch" => CliCommand.ExportPatch,
    "git" => CliCommand.Git,
    "svn" => CliCommand.Svn,
    "help" => CliCommand.Help,
    "version" => CliCommand.Version,
    _ => null,
  };

  private static bool TryParseFormat(string text, out OutputFormat format) {
    format = OutputFormat.Semantic;
    return text.ToLowerInvariant() switch {
      "semantic" => Assign(OutputFormat.Semantic, out format),
      "unified" => Assign(OutputFormat.Unified, out format),
      "git" => Assign(OutputFormat.Git, out format),
      "svn" => Assign(OutputFormat.Svn, out format),
      "json" => Assign(OutputFormat.Json, out format),
      _ => false,
    };

    static bool Assign(OutputFormat value, out OutputFormat target) {
      target = value;
      return true;
    }
  }

  // Accepts both `--flag value` and `--flag=value`.
  private static bool TryReadValue(IReadOnlyList<string> args, ref int index, string name, out string value) {
    var argument = args[index];
    if (argument == name) {
      if (index + 1 >= args.Count) {
        value = string.Empty;
        index = args.Count;
        return true;
      }

      value = args[++index];
      return true;
    }

    if (argument.StartsWith(name + "=", StringComparison.Ordinal)) {
      value = argument[(name.Length + 1)..];
      return true;
    }

    value = string.Empty;
    return false;
  }
}
