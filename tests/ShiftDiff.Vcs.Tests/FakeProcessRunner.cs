using ShiftDiff.Vcs;

namespace ShiftDiff.Vcs.Tests;

// Records every invocation and replays canned output, so provider behaviour is
// pinned without a real repository or a real git/svn installation.
public sealed class FakeProcessRunner : IProcessRunner {
  private readonly List<Func<IReadOnlyList<string>, ProcessResult?>> _responses = [];

  public List<(string Executable, string[] Arguments, string? WorkingDirectory)> Invocations { get; } = [];

  public string LastCommandLine => Invocations.Count == 0
      ? string.Empty
      : $"{Invocations[^1].Executable} {string.Join(' ', Invocations[^1].Arguments)}";

  public FakeProcessRunner Respond(string argumentContains, string standardOutput, int exitCode = 0) {
    _responses.Add(arguments => string.Join(' ', arguments).Contains(argumentContains, StringComparison.Ordinal)
        ? new ProcessResult(exitCode, standardOutput, string.Empty)
        : null);
    return this;
  }

  public FakeProcessRunner RespondWithFailure(string argumentContains, string standardError, int exitCode = 1) {
    _responses.Add(arguments => string.Join(' ', arguments).Contains(argumentContains, StringComparison.Ordinal)
        ? new ProcessResult(exitCode, string.Empty, standardError)
        : null);
    return this;
  }

  public ProcessResult Run(string executable, IReadOnlyList<string> arguments, string? workingDirectory = null) {
    Invocations.Add((executable, arguments.ToArray(), workingDirectory));
    foreach (var response in _responses) {
      if (response(arguments) is { } result) return result;
    }

    return new ProcessResult(0, string.Empty, string.Empty);
  }
}
