namespace ShiftDiff.Core;

public static class UnifiedDiffFormatter
{
    public static IReadOnlyList<string> Format(UnifiedDiffFile file)
    {
        if (file.GitHeader is not null)
        {
            throw new NotSupportedException(
                "Formatting a UnifiedDiffFile with a git extended header is not supported in this slice.");
        }

        var lines = new List<string>
        {
            FormatFileHeaderLine("---", file.Header.SourcePath, file.Header.SourceRevision),
            FormatFileHeaderLine("+++", file.Header.TargetPath, file.Header.TargetRevision),
        };

        foreach (var hunk in file.Hunks)
        {
            lines.Add(FormatHunkHeader(hunk.Header));
            foreach (var line in hunk.Lines)
            {
                lines.Add(FormatLine(line));
            }
        }

        return lines;
    }

    public static IReadOnlyList<string> Format(UnifiedDiffPatch patch)
    {
        var lines = new List<string>();
        foreach (var file in patch.Files)
        {
            lines.AddRange(Format(file));
        }

        return lines;
    }

    private static string FormatFileHeaderLine(string marker, string path, string? revision)
    {
        return revision is null ? $"{marker} {path}" : $"{marker} {path}\t{revision}";
    }

    private static string FormatHunkHeader(UnifiedDiffHunkHeader header)
    {
        return $"@@ -{header.OldStart},{header.OldCount} +{header.NewStart},{header.NewCount} @@";
    }

    private static string FormatLine(UnifiedDiffLine line)
    {
        var marker = line.Kind switch
        {
            UnifiedDiffLineKind.Context => ' ',
            UnifiedDiffLineKind.Added => '+',
            UnifiedDiffLineKind.Removed => '-',
            _ => throw new ArgumentOutOfRangeException(nameof(line), line.Kind, "Unrecognized unified diff line kind."),
        };

        return $"{marker}{line.Content}";
    }
}
