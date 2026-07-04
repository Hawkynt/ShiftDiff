using System.Text.Json;

namespace ShiftDiff.Core;

public enum JsonChangeType
{
    Added,
    Removed,
    Changed,
    Unchanged,
}

public sealed record JsonChange(string Path, JsonChangeType ChangeType, string? OldValue = null, string? NewValue = null);

public static class JsonComparer
{
    public static JsonChange[] Compare(byte[] baseJson, byte[] targetJson)
    {
        using var baseDocument = JsonDocument.Parse(baseJson);
        using var targetDocument = JsonDocument.Parse(targetJson);

        var baseProperties = baseDocument.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetRawText());
        var targetProperties = targetDocument.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetRawText());

        var keys = baseProperties.Keys.Union(targetProperties.Keys).OrderBy(k => k, StringComparer.Ordinal);

        var changes = new List<JsonChange>();
        foreach (var key in keys)
        {
            var hasOld = baseProperties.TryGetValue(key, out var oldValue);
            var hasNew = targetProperties.TryGetValue(key, out var newValue);

            var changeType = (hasOld, hasNew) switch
            {
                (false, true) => JsonChangeType.Added,
                (true, false) => JsonChangeType.Removed,
                (true, true) when oldValue == newValue => JsonChangeType.Unchanged,
                _ => JsonChangeType.Changed,
            };

            changes.Add(new JsonChange(key, changeType, oldValue, newValue));
        }

        return changes.ToArray();
    }
}
