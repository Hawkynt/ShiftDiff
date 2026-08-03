using System.Diagnostics;
using System.Text;

namespace ShiftDiff.Vcs;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

// The single seam between the VCS providers and the outside world: everything
// else in this assembly is pure parsing, so provider behaviour is testable
// without a repository on disk.
public interface IProcessRunner
{
    ProcessResult Run(string executable, IReadOnlyList<string> arguments, string? workingDirectory = null);
}

public sealed class VcsExecutableMissingException(string executable)
    : Exception($"'{executable}' was not found on PATH")
{
    public string Executable { get; } = executable;
}

public sealed class SystemProcessRunner(TimeSpan? timeout = null) : IProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    public static SystemProcessRunner Instance { get; } = new();

    public TimeSpan Timeout { get; } = timeout ?? DefaultTimeout;

    public ProcessResult Run(string executable, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        // A VCS must never sit waiting for a terminal that is not there.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "never";

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new VcsExecutableMissingException(executable);

            // Both pipes are drained concurrently: git happily writes a page of
            // usage text to stderr, and reading stdout to the end first would
            // deadlock as soon as the stderr buffer fills up.
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            process.StandardInput.Close();

            if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
            {
                TryKill(process);
                throw new VcsCommandException(
                    $"'{executable} {string.Join(' ', arguments)}' did not finish within {Timeout.TotalSeconds:N0}s");
            }

            // Lets the redirected streams flush before their results are read.
            process.WaitForExit();
            return new ProcessResult(process.ExitCode, standardOutput.Result, standardError.Result);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new VcsExecutableMissingException(executable);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            // The process ended on its own between the timeout and the kill.
        }
    }
}
