namespace ShiftDiff.Core;

public static class FolderChangeFilter
{
    public static FolderEntryChange[] ByExtension(
        IReadOnlyList<FolderEntryChange> changes,
        params string[] extensions)
    {
        if (extensions.Length == 0)
        {
            return changes.ToArray();
        }

        return changes
            .Where(c => extensions.Any(ext =>
                c.RelativePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public static FolderEntryChange[] ByPathPrefix(
        IReadOnlyList<FolderEntryChange> changes,
        params string[] prefixes)
    {
        if (prefixes.Length == 0)
        {
            return changes.ToArray();
        }

        return changes
            .Where(c => prefixes.Any(p =>
                c.RelativePath.StartsWith(p, StringComparison.Ordinal)))
            .ToArray();
    }

    public static FolderEntryChange[] BySize(
        IReadOnlyList<FolderEntryChange> changes,
        long? minSize = null,
        long? maxSize = null)
    {
        if (minSize is null && maxSize is null)
        {
            return changes.ToArray();
        }

        return changes
            .Where(c => c.Size is long size
                && (minSize is null || size >= minSize)
                && (maxSize is null || size <= maxSize))
            .ToArray();
    }
}
