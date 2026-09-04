using ShiftDiff.Vcs;

namespace ShiftDiff.Vcs.Tests;

// These run a real child process: the failure they guard against — a tool that
// fills the stderr pipe while the runner waits on stdout — only reproduces
// against a real pipe.
public class SystemProcessRunnerTests {
  private static readonly bool OnWindows = OperatingSystem.IsWindows();

  [Fact]
  public void Run_CommandWritingToStdout_CapturesIt() {
    var result = Shell("echo hello");

    Assert.True(result.Succeeded);
    Assert.Contains("hello", result.StandardOutput);
  }

  // Regression: git answers an invalid invocation with a page of usage text on
  // stderr. Draining stdout first blocked forever once that pipe filled up.
  [Fact]
  public void Run_CommandFloodingStderr_DoesNotDeadlock() {
    var result = Shell(OnWindows
        ? "for /L %i in (1,1,4000) do @echo aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa 1>&2"
        : "i=0; while [ $i -lt 4000 ]; do echo aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa >&2; i=$((i+1)); done");

    Assert.True(result.StandardError.Length > 100_000, "the child wrote far more than one pipe buffer");
  }

  [Fact]
  public void Run_CommandFloodingStdout_DoesNotDeadlock() {
    var result = Shell(OnWindows
        ? "for /L %i in (1,1,4000) do @echo bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
        : "i=0; while [ $i -lt 4000 ]; do echo bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb; i=$((i+1)); done");

    Assert.True(result.StandardOutput.Length > 100_000);
  }

  [Fact]
  public void Run_FailingCommand_ReportsTheExitCodeInsteadOfThrowing() {
    var result = Shell(OnWindows ? "exit 3" : "exit 3");

    Assert.False(result.Succeeded);
    Assert.Equal(3, result.ExitCode);
  }

  [Fact]
  public void Run_CommandThatNeverEnds_IsKilledAtTheTimeout() {
    var runner = new SystemProcessRunner(TimeSpan.FromSeconds(2));

    var exception = Assert.Throws<VcsCommandException>(() => runner.Run(
        ShellExecutable,
        ShellArguments(OnWindows ? "ping -n 30 127.0.0.1 > nul" : "sleep 30"),
        Path.GetTempPath()));

    Assert.Contains("did not finish", exception.Message);
  }

  [Fact]
  public void Run_MissingExecutable_ReportsItByName() {
    var exception = Assert.Throws<VcsExecutableMissingException>(
        () => SystemProcessRunner.Instance.Run("shiftdiff-no-such-tool", ["--version"]));

    Assert.Equal("shiftdiff-no-such-tool", exception.Executable);
  }

  [Fact]
  public void Run_UsesTheGivenWorkingDirectory() {
    var directory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "shiftdiff-cwd", Guid.NewGuid().ToString("N"))).FullName;
    try {
      var result = Shell(OnWindows ? "cd" : "pwd", directory);

      Assert.Contains(Path.GetFileName(directory), result.StandardOutput);
    } finally {
      Directory.Delete(directory, recursive: true);
    }
  }

  private static string ShellExecutable => OnWindows ? "cmd.exe" : "/bin/sh";

  private static string[] ShellArguments(string command) => OnWindows ? ["/c", command] : ["-c", command];

  private static ProcessResult Shell(string command, string? workingDirectory = null) =>
      new SystemProcessRunner(TimeSpan.FromSeconds(30))
          .Run(ShellExecutable, ShellArguments(command), workingDirectory ?? Path.GetTempPath());
}
