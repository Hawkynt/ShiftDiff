namespace ShiftDiff.Core;

public static class LineDiffer
{
    public static LineChange[] Diff(string[] oldLines, string[] newLines)
    {
        var resultLength = Math.Max(oldLines.Length, newLines.Length);
        var result = new LineChange[resultLength];

        for (var index = 0; index < resultLength; index++)
        {
            result[index] = new LineChange(ClassifyLine(oldLines, newLines, index));
        }

        return result;
    }

    private static ChangeType ClassifyLine(string[] oldLines, string[] newLines, int index)
    {
        if (index >= oldLines.Length)
        {
            return ChangeType.Added;
        }

        if (index >= newLines.Length)
        {
            return ChangeType.Removed;
        }

        return oldLines[index] == newLines[index]
            ? ChangeType.Unchanged
            : ChangeType.Edited;
    }
}
