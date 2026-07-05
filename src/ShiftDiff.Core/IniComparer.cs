using System.Text;

namespace ShiftDiff.Core;

public enum IniChangeType
{
    Added,
    Removed,
    Changed,
    Unchanged,
}

public sealed record IniChange(string Path, IniChangeType ChangeType, string? OldValue = null, string? NewValue = null);

public static class IniComparer
{
    public static IniChange[] Compare(byte[] baseIni, byte[] targetIni)
    {
        var baseEntries = Parse(Encoding.UTF8.GetString(baseIni));
        var targetEntries = Parse(Encoding.UTF8.GetString(targetIni));

        var paths = baseEntries.Keys.Union(targetEntries.Keys).OrderBy(p => p, StringComparer.Ordinal);

        var changes = new List<IniChange>();
        foreach (var path in paths)
        {
            var hasOld = baseEntries.TryGetValue(path, out var oldValue);
            var hasNew = targetEntries.TryGetValue(path, out var newValue);

            var changeType = (hasOld, hasNew) switch
            {
                (false, true) => IniChangeType.Added,
                (true, false) => IniChangeType.Removed,
                (true, true) when oldValue == newValue => IniChangeType.Unchanged,
                _ => IniChangeType.Changed,
            };

            changes.Add(new IniChange(path, changeType, hasOld ? oldValue : null, hasNew ? newValue : null));
        }

        return changes.ToArray();
    }

    private static Dictionary<string, string> Parse(string text)
    {
        var entries = new Dictionary<string, string>();
        string? section = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');

            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            var escapedKey = key.Replace(".", "\\.");
            var path = section is null ? escapedKey : $"{section.Replace(".", "\\.")}.{escapedKey}";
            entries[path] = value;
        }

        return entries;
    }
}
