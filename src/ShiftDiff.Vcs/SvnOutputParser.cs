using System.Globalization;
using System.Xml.Linq;

namespace ShiftDiff.Vcs;

// Pure parsing of `svn status --xml` / `svn log --xml`. XML is used rather than
// the human-readable form because svn's plain output is locale-dependent.
public static class SvnOutputParser
{
    public static IReadOnlyList<VcsFileStatus> ParseStatus(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return [];

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return ParsePlainStatus(xml);
        }

        var result = new List<VcsFileStatus>();
        foreach (var entry in document.Descendants("entry"))
        {
            var path = entry.Attribute("path")?.Value;
            if (path is null) continue;

            var status = entry.Element("wc-status");
            var item = status?.Attribute("item")?.Value ?? "normal";
            var copiedFrom = status?.Attribute("copied")?.Value == "true"
                ? entry.Element("wc-status")?.Attribute("moved-from")?.Value
                : null;

            result.Add(new VcsFileStatus(Normalize(path), ToChangeKind(item), OriginalPath: copiedFrom));
        }

        return result;
    }

    public static IReadOnlyList<VcsFileStatus> ParsePlainStatus(string output)
    {
        var result = new List<VcsFileStatus>();
        foreach (var line in SplitLines(output))
        {
            if (line.Length < 2) continue;

            var kind = line[0] switch
            {
                'A' => VcsChangeKind.Added,
                'M' => VcsChangeKind.Modified,
                'D' => VcsChangeKind.Deleted,
                'R' => VcsChangeKind.Modified,
                'C' => VcsChangeKind.Conflicted,
                '?' => VcsChangeKind.Untracked,
                'I' => VcsChangeKind.Ignored,
                '!' => VcsChangeKind.Deleted,
                _ => VcsChangeKind.Unchanged,
            };

            if (kind == VcsChangeKind.Unchanged) continue;

            result.Add(new VcsFileStatus(Normalize(line[1..].Trim()), kind));
        }

        return result;
    }

    public static IReadOnlyList<VcsRevision> ParseLog(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return [];

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        var result = new List<VcsRevision>();
        foreach (var entry in document.Descendants("logentry"))
        {
            var revision = entry.Attribute("revision")?.Value ?? string.Empty;
            var author = entry.Element("author")?.Value ?? string.Empty;
            var message = entry.Element("msg")?.Value ?? string.Empty;
            var timestampText = entry.Element("date")?.Value;
            var timestamp = DateTimeOffset.TryParse(
                timestampText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : default;

            result.Add(new VcsRevision(revision, author, timestamp, message.Trim()));
        }

        return result;
    }

    private static VcsChangeKind ToChangeKind(string item) => item switch
    {
        "added" => VcsChangeKind.Added,
        "modified" => VcsChangeKind.Modified,
        "replaced" => VcsChangeKind.Modified,
        "deleted" => VcsChangeKind.Deleted,
        "missing" => VcsChangeKind.Deleted,
        "conflicted" => VcsChangeKind.Conflicted,
        "unversioned" => VcsChangeKind.Untracked,
        "ignored" => VcsChangeKind.Ignored,
        _ => VcsChangeKind.Unchanged,
    };

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static IEnumerable<string> SplitLines(string output) =>
        output.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
