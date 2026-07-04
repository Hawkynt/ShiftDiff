using System.Text;

namespace ShiftDiff.Core;

public enum LineEnding { Lf, CrLf, Cr, Mixed }

public sealed record TextFileContent(string[] Lines, Encoding Encoding, LineEnding OriginalEnding);

public static class TextFileLoader
{
    public static TextFileContent Load(byte[] content)
    {
        var (encoding, bomLength) = DetectEncoding(content);
        var text = encoding.GetString(content, bomLength, content.Length - bomLength);
        var (lines, ending) = SplitLines(text);
        return new TextFileContent(lines, encoding, ending);
    }

    // BOM sniff only (no statistical/charset guessing — SPEC doesn't ask
    // for that level of detection). UTF-32 checked before UTF-16 since the
    // UTF-16LE BOM bytes are a prefix of the UTF-32LE BOM.
    private static (Encoding Encoding, int BomLength) DetectEncoding(byte[] content)
    {
        if (StartsWith(content, 0xFF, 0xFE, 0x00, 0x00))
        {
            return (new UTF32Encoding(bigEndian: false, byteOrderMark: false), 4);
        }

        if (StartsWith(content, 0x00, 0x00, 0xFE, 0xFF))
        {
            return (new UTF32Encoding(bigEndian: true, byteOrderMark: false), 4);
        }

        if (StartsWith(content, 0xEF, 0xBB, 0xBF))
        {
            return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 3);
        }

        if (StartsWith(content, 0xFF, 0xFE))
        {
            return (Encoding.Unicode, 2);
        }

        if (StartsWith(content, 0xFE, 0xFF))
        {
            return (Encoding.BigEndianUnicode, 2);
        }

        return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 0);
    }

    private static bool StartsWith(byte[] content, params byte[] prefix)
    {
        if (content.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (content[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }

    // Scans once, classifying each terminator run (\r\n, bare \n, bare \r)
    // as it goes — the only piece here that isn't a trivial pass-through,
    // since a later patch-export/reconstruct step needs the original style
    // to round-trip losslessly.
    private static (string[] Lines, LineEnding Ending) SplitLines(string text)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        var sawCrLf = false;
        var sawLf = false;
        var sawCr = false;

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    sawCrLf = true;
                    i++;
                }
                else
                {
                    sawCr = true;
                }

                lines.Add(current.ToString());
                current.Clear();
                continue;
            }

            if (character == '\n')
            {
                sawLf = true;
                lines.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0 || lines.Count == 0)
        {
            lines.Add(current.ToString());
        }

        var stylesSeen = (sawCrLf ? 1 : 0) + (sawLf ? 1 : 0) + (sawCr ? 1 : 0);
        var ending = stylesSeen > 1 ? LineEnding.Mixed
            : sawCrLf ? LineEnding.CrLf
            : sawCr ? LineEnding.Cr
            : LineEnding.Lf;

        return (lines.ToArray(), ending);
    }
}
