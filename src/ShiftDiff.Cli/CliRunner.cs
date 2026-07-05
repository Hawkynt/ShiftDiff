using ShiftDiff.Core;

namespace ShiftDiff.Cli;

public static class CliRunner
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 2)
        {
            error.WriteLine("usage: shiftdiff <old-file> <new-file>");
            return 1;
        }

        var oldPath = args[0];
        var newPath = args[1];

        byte[] oldContent;
        byte[] newContent;
        try
        {
            oldContent = File.ReadAllBytes(oldPath);
            newContent = File.ReadAllBytes(newPath);
        }
        catch (IOException ex)
        {
            error.WriteLine($"error: {ex.Message}");
            return 1;
        }

        var result = FileComparer.Compare(oldContent, newContent);
        var unifiedDiffFile = UnifiedDiffBuilder.Build(result.Changes, oldPath, newPath);

        foreach (var line in UnifiedDiffFormatter.Format(unifiedDiffFile))
        {
            output.WriteLine(line);
        }

        return 0;
    }
}
