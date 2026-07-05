using System.Text;
using System.Xml;
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
        XElement baseRoot;
        XElement targetRoot;
        try
        {
            baseRoot = XDocument.Parse(Encoding.UTF8.GetString(baseXml)).Root!;
            targetRoot = XDocument.Parse(Encoding.UTF8.GetString(targetXml)).Root!;
        }
        catch (XmlException)
        {
            var oldRawText = Encoding.UTF8.GetString(baseXml);
            var newRawText = Encoding.UTF8.GetString(targetXml);
            return [new XmlChange(null, oldRawText == newRawText ? XmlChangeType.Unchanged : XmlChangeType.Changed, oldRawText, newRawText)];
        }

        if (baseRoot.Name != targetRoot.Name)
        {
            return [new XmlChange(null, XmlChangeType.Changed, baseRoot.Name.LocalName, targetRoot.Name.LocalName)];
        }

        var changes = new List<XmlChange>();
        CompareElements(baseRoot, targetRoot, path: null, changes);
        return changes.ToArray();
    }

    private static void CompareElements(XElement baseElement, XElement targetElement, string? path, List<XmlChange> changes)
    {
        CompareAttributes(baseElement, targetElement, path, changes);

        // Text content only counts when neither side has child elements — an element with children has its
        // structure captured by the child-recursion below, and any surrounding text is formatting whitespace.
        if (!baseElement.Elements().Any() && !targetElement.Elements().Any())
        {
            CompareText(baseElement, targetElement, path, changes);
        }

        // Repeated same-name child elements (e.g. lists) are skipped, not thrown on — list/positional diffing is a separate, deferred slice.
        var baseChildren = baseElement.Elements().GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.ToArray());
        var targetChildren = targetElement.Elements().GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.ToArray());

        var childNames = baseChildren.Keys.Union(targetChildren.Keys).OrderBy(n => n, StringComparer.Ordinal);

        foreach (var name in childNames)
        {
            var hasOld = baseChildren.TryGetValue(name, out var oldGroup);
            var hasNew = targetChildren.TryGetValue(name, out var newGroup);

            if ((hasOld && oldGroup!.Length > 1) || (hasNew && newGroup!.Length > 1))
            {
                continue;
            }

            var oldChild = hasOld ? oldGroup![0] : null;
            var newChild = hasNew ? newGroup![0] : null;
            var childPath = path is null ? name : $"{path}/{name}";

            if (hasOld && hasNew)
            {
                CompareElements(oldChild!, newChild!, childPath, changes);
                continue;
            }

            var changeType = hasNew ? XmlChangeType.Added : XmlChangeType.Removed;
            changes.Add(new XmlChange(childPath, changeType, hasOld ? oldChild!.ToString() : null, hasNew ? newChild!.ToString() : null));
        }
    }

    private static void CompareAttributes(XElement baseElement, XElement targetElement, string? path, List<XmlChange> changes)
    {
        var baseAttributes = baseElement.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
        var targetAttributes = targetElement.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);

        var names = baseAttributes.Keys.Union(targetAttributes.Keys).OrderBy(n => n, StringComparer.Ordinal);

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

            var attributePath = path is null ? $"@{name}" : $"{path}/@{name}";
            changes.Add(new XmlChange(attributePath, changeType, hasOld ? oldValue : null, hasNew ? newValue : null));
        }
    }

    private static void CompareText(XElement baseElement, XElement targetElement, string? path, List<XmlChange> changes)
    {
        var oldText = baseElement.Value;
        var newText = targetElement.Value;

        if (oldText.Length == 0 && newText.Length == 0)
        {
            return;
        }

        var changeType = (oldText.Length, newText.Length) switch
        {
            (0, > 0) => XmlChangeType.Added,
            (> 0, 0) => XmlChangeType.Removed,
            _ when oldText == newText => XmlChangeType.Unchanged,
            _ => XmlChangeType.Changed,
        };

        changes.Add(new XmlChange(path, changeType, oldText.Length == 0 ? null : oldText, newText.Length == 0 ? null : newText));
    }
}
