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

public sealed class SystemProcessRunner : IProcessRunner
{
    public static SystemProcessRunner Instance { get; } = new();

    public ProcessResult Run(string executable, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new VcsExecutableMissingException(executable);
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new ProcessResult(process.ExitCode, standardOutput, standardError);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new VcsExecutableMissingException(executable);
        }
    }
}
