using ShiftDiff.Core;

namespace ShiftDiff.Cli;

public static class CliRunner
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 4 && args[0] == "--patch" && args[2] == "--source")
        {
            return RunPatchMode(args[1], args[3], output, error);
        }

        if (args.Length != 2)
        {
            error.WriteLine("usage: shiftdiff <old-file> <new-file>");
            error.WriteLine("       shiftdiff --patch <patch-file> --source <source-file>");
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

    private static int RunPatchMode(string patchPath, string sourcePath, TextWriter output, TextWriter error)
    {
        string[] patchLines;
        string[] sourceLines;
        try
        {
            patchLines = File.ReadAllLines(patchPath);
            sourceLines = File.ReadAllLines(sourcePath);
        }
        catch (IOException ex)
        {
            error.WriteLine($"error: {ex.Message}");
            return 1;
        }

        var patch = UnifiedDiffParser.ParsePatch(patchLines);
        if (patch.Files.Count == 0)
        {
            error.WriteLine("error: patch contains no file entries");
            return 1;
        }

        IReadOnlyList<string> resultLines;
        try
        {
            resultLines = PatchApplier.ApplyFileExact(sourceLines, patch.Files[0]);
        }
        catch (PatchApplicationException ex)
        {
            error.WriteLine($"error: {ex.Message}");
            return 1;
        }

        foreach (var line in resultLines)
        {
            output.WriteLine(line);
        }

        return 0;
    }
}
