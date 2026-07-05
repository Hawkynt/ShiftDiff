using System.Text;
using System.Text.Json;

namespace ShiftDiff.Core;

public enum JsonChangeType
{
    Added,
    Removed,
    Changed,
    Unchanged,
}

public sealed record JsonChange(string? Path, JsonChangeType ChangeType, string? OldValue = null, string? NewValue = null);

public static class JsonComparer
{
    public static JsonChange[] Compare(byte[] baseJson, byte[] targetJson)
    {
        JsonDocument? baseDocument = null;
        try
        {
            baseDocument = JsonDocument.Parse(baseJson);
            using var targetDocument = JsonDocument.Parse(targetJson);

            var changes = new List<JsonChange>();
            CompareValues(baseDocument.RootElement, targetDocument.RootElement, path: null, changes);
            return changes.ToArray();
        }
        catch (JsonException)
        {
            var oldRawText = Encoding.UTF8.GetString(baseJson);
            var newRawText = Encoding.UTF8.GetString(targetJson);
            return [new JsonChange(null, oldRawText == newRawText ? JsonChangeType.Unchanged : JsonChangeType.Changed, oldRawText, newRawText)];
        }
        finally
        {
            baseDocument?.Dispose();
        }
    }

    private static void CompareValues(JsonElement baseValue, JsonElement targetValue, string? path, List<JsonChange> changes)
    {
        if (baseValue.ValueKind == JsonValueKind.Object && targetValue.ValueKind == JsonValueKind.Object)
        {
            CompareObjects(baseValue, targetValue, path, changes);
            return;
        }

        var oldRawValue = baseValue.GetRawText();
        var newRawValue = targetValue.GetRawText();
        changes.Add(new JsonChange(path, oldRawValue == newRawValue ? JsonChangeType.Unchanged : JsonChangeType.Changed, oldRawValue, newRawValue));
    }

    private static void CompareObjects(JsonElement baseObject, JsonElement targetObject, string? path, List<JsonChange> changes)
    {
        var baseProperties = baseObject.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var targetProperties = targetObject.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        var keys = baseProperties.Keys.Union(targetProperties.Keys).OrderBy(k => k, StringComparer.Ordinal);

        foreach (var key in keys)
        {
            var hasOld = baseProperties.TryGetValue(key, out var oldElement);
            var hasNew = targetProperties.TryGetValue(key, out var newElement);
            var childPath = path is null ? key : $"{path}.{key}";

            if (hasOld && hasNew
                && oldElement.ValueKind == JsonValueKind.Object
                && newElement.ValueKind == JsonValueKind.Object)
            {
                CompareObjects(oldElement, newElement, childPath, changes);
                continue;
            }

            var oldValue = hasOld ? oldElement.GetRawText() : null;
            var newValue = hasNew ? newElement.GetRawText() : null;

            var changeType = (hasOld, hasNew) switch
            {
                (false, true) => JsonChangeType.Added,
                (true, false) => JsonChangeType.Removed,
                (true, true) when oldValue == newValue => JsonChangeType.Unchanged,
                _ => JsonChangeType.Changed,
            };

            changes.Add(new JsonChange(childPath, changeType, oldValue, newValue));
        }
    }
}
