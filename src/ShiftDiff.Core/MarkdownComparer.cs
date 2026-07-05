using System.Text;

namespace ShiftDiff.Core;

public enum MarkdownChangeType
{
    Added,
    Removed,
    Changed,
    Unchanged,
    Moved,
    MovedEdited,
}

public sealed record MarkdownChange(string Path, MarkdownChangeType ChangeType, string? OldValue = null, string? NewValue = null, LineChange[]? BodyChanges = null, string? MovedFrom = null);

public static class MarkdownComparer
{
    public static MarkdownChange[] Compare(byte[] baseMarkdown, byte[] targetMarkdown)
    {
        var baseSections = Parse(Encoding.UTF8.GetString(baseMarkdown));
        var targetSections = Parse(Encoding.UTF8.GetString(targetMarkdown));

        var paths = baseSections.Keys.Union(targetSections.Keys).OrderBy(p => p, StringComparer.Ordinal);

        var changes = new List<MarkdownChange>();
        foreach (var path in paths)
        {
            var hasOld = baseSections.TryGetValue(path, out var oldValue);
            var hasNew = targetSections.TryGetValue(path, out var newValue);

            var changeType = (hasOld, hasNew) switch
            {
                (false, true) => MarkdownChangeType.Added,
                (true, false) => MarkdownChangeType.Removed,
                (true, true) when oldValue == newValue => MarkdownChangeType.Unchanged,
                _ => MarkdownChangeType.Changed,
            };

            var bodyChanges = changeType == MarkdownChangeType.Changed
                ? LineDiffer.Diff(oldValue!.Split('\n'), newValue!.Split('\n'))
                : null;

            changes.Add(new MarkdownChange(path, changeType, hasOld ? oldValue : null, hasNew ? newValue : null, bodyChanges));
        }

        return changes.ToArray();
    }

    private static Dictionary<string, string> Parse(string text)
    {
        var sections = new Dictionary<string, string>();
        var headingStack = new List<string>();
        var currentKey = "";
        var currentContent = new List<string>();
        var insideCodeFence = false;

        void Flush()
        {
            var content = string.Join("\n", currentContent).Trim();
            if (currentKey.Length == 0 && content.Length == 0)
            {
                return;
            }

            sections[currentKey] = content;
        }

        void ApplyHeading(string headingLine, int level)
        {
            if (headingStack.Count >= level)
            {
                headingStack.RemoveRange(level - 1, headingStack.Count - (level - 1));
            }

            while (headingStack.Count < level - 1)
            {
                headingStack.Add("");
            }

            headingStack.Add(headingLine);
            currentKey = string.Join(" > ", headingStack.Where(s => s.Length > 0));
            currentContent = new List<string>();
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (IsCodeFenceDelimiter(line))
            {
                insideCodeFence = !insideCodeFence;
                currentContent.Add(line);
                continue;
            }

            if (!insideCodeFence && TryGetSetextLevel(line, out var setextLevel)
                && currentContent.Count > 0 && currentContent[^1].Trim().Length > 0)
            {
                var headingText = currentContent[^1].Trim();
                currentContent.RemoveAt(currentContent.Count - 1);
                Flush();
                ApplyHeading(new string('#', setextLevel) + " " + headingText, setextLevel);
                continue;
            }

            if (!insideCodeFence && TryGetHeadingLevel(line, out var level))
            {
                Flush();
                ApplyHeading(line.Trim(), level);
                continue;
            }

            currentContent.Add(line);
        }

        Flush();

        return sections;
    }

    private static bool TryGetSetextLevel(string line, out int level)
    {
        var trimmed = line.Trim();

        if (trimmed.Length > 0 && trimmed.All(c => c == '='))
        {
            level = 1;
            return true;
        }

        if (trimmed.Length > 0 && trimmed.All(c => c == '-'))
        {
            level = 2;
            return true;
        }

        level = 0;
        return false;
    }

    private static bool IsCodeFenceDelimiter(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static bool TryGetHeadingLevel(string line, out int level)
    {
        var trimmed = line.TrimStart();
        var hashCount = 0;
        while (hashCount < trimmed.Length && trimmed[hashCount] == '#')
        {
            hashCount++;
        }

        if (hashCount is >= 1 and <= 6 && hashCount < trimmed.Length && trimmed[hashCount] == ' ')
        {
            level = hashCount;
            return true;
        }

        level = 0;
        return false;
    }
}
