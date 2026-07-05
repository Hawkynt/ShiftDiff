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

        if (args.Length == 3)
        {
            return RunThreeWayMode(args[0], args[1], args[2], output, error);
        }

        if (args.Length != 2)
        {
            error.WriteLine("usage: shiftdiff <old-file> <new-file>");
            error.WriteLine("       shiftdiff --patch <patch-file> --source <source-file>");
            error.WriteLine("       shiftdiff <base-file> <local-file> <remote-file>");
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"error: {ex.Message}");
            return 1;
        }

        if (HasMatchingExtension(oldPath, newPath, ".ini"))
        {
            foreach (var line in IniChangeFormatter.Format(IniComparer.Compare(oldContent, newContent)))
            {
                output.WriteLine(line);
            }

            return 0;
        }

        if (HasMatchingExtension(oldPath, newPath, ".json"))
        {
            foreach (var line in JsonChangeFormatter.Format(JsonComparer.Compare(oldContent, newContent)))
            {
                output.WriteLine(line);
            }

            return 0;
        }

        if (HasMatchingExtension(oldPath, newPath, ".xml"))
        {
            foreach (var line in XmlChangeFormatter.Format(XmlComparer.Compare(oldContent, newContent)))
            {
                output.WriteLine(line);
            }

            return 0;
        }

        if (HasMatchingExtension(oldPath, newPath, ".md"))
        {
            var markdownChanges = MarkdownMoveDetector.Detect(MarkdownComparer.Compare(oldContent, newContent));
            foreach (var line in MarkdownChangeFormatter.Format(markdownChanges))
            {
                output.WriteLine(line);
            }

            return 0;
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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

    private static int RunThreeWayMode(
        string basePath, string localPath, string remotePath, TextWriter output, TextWriter error)
    {
        string[] baseLines;
        string[] localLines;
        string[] remoteLines;
        try
        {
            baseLines = File.ReadAllLines(basePath);
            localLines = File.ReadAllLines(localPath);
            remoteLines = File.ReadAllLines(remotePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"error: {ex.Message}");
            return 1;
        }

        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);
        var conflictCount = 0;
        var i = 0;
        while (i < changes.Length)
        {
            if (changes[i].ChangeType != ChangeType.Conflict)
            {
                WriteResolvedLine(changes[i], output);
                i++;
                continue;
            }

            conflictCount++;
            var localBlock = new List<string>();
            var remoteBlock = new List<string>();
            while (i < changes.Length && changes[i].ChangeType == ChangeType.Conflict)
            {
                if (changes[i].LocalLine is not null)
                {
                    localBlock.Add(changes[i].LocalLine!);
                }

                if (changes[i].RemoteLine is not null)
                {
                    remoteBlock.Add(changes[i].RemoteLine!);
                }

                i++;
            }

            output.WriteLine("<<<<<<< local");
            foreach (var line in localBlock)
            {
                output.WriteLine(line);
            }

            output.WriteLine("=======");
            foreach (var line in remoteBlock)
            {
                output.WriteLine(line);
            }

            output.WriteLine(">>>>>>> remote");
        }

        if (conflictCount > 0)
        {
            error.WriteLine($"{conflictCount} conflict(s) require resolution");
            return 1;
        }

        return 0;
    }

    private static bool HasMatchingExtension(string oldPath, string newPath, string extension) =>
        string.Equals(Path.GetExtension(oldPath), extension, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Path.GetExtension(newPath), extension, StringComparison.OrdinalIgnoreCase);

    private static void WriteResolvedLine(ThreeWayChange change, TextWriter output)
    {
        switch (change.ChangeType)
        {
            case ChangeType.Removed:
                break;
            case ChangeType.Unchanged:
                output.WriteLine(change.BaseLine!);
                break;
            default:
                output.WriteLine(change.LocalLine ?? change.RemoteLine!);
                break;
        }
    }
}
