using System.Text;
using System.Xml.Linq;

namespace ShiftDiff.Core;

public enum XmlChangeType
{
    Added,
    Removed,
    Changed,
    Unchanged,
}

public sealed record XmlChange(string? Path, XmlChangeType ChangeType, string? OldValue = null, string? NewValue = null);

public static class XmlComparer
{
    public static XmlChange[] Compare(byte[] baseXml, byte[] targetXml)
    {
        var baseRoot = XDocument.Parse(Encoding.UTF8.GetString(baseXml)).Root!;
        var targetRoot = XDocument.Parse(Encoding.UTF8.GetString(targetXml)).Root!;

        var baseAttributes = baseRoot.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
        var targetAttributes = targetRoot.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);

        var names = baseAttributes.Keys.Union(targetAttributes.Keys).OrderBy(n => n, StringComparer.Ordinal);

        var changes = new List<XmlChange>();
        foreach (var name in names)
        {
            var hasOld = baseAttributes.TryGetValue(name, out var oldValue);
            var hasNew = targetAttributes.TryGetValue(name, out var newValue);

            var changeType = (hasOld, hasNew) switch
            {
                (false, true) => XmlChangeType.Added,
                (true, false) => XmlChangeType.Removed,
                (true, true) when oldValue == newValue => XmlChangeType.Unchanged,
                _ => XmlChangeType.Changed,
            };

            changes.Add(new XmlChange($"@{name}", changeType, hasOld ? oldValue : null, hasNew ? newValue : null));
        }

        return changes.ToArray();
    }
}
