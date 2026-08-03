using System.Text;
using System.Text.Json;
using ShiftDiff.Core;

namespace ShiftDiff.Cli;

// SPEC section 12: machine-readable output for automation. Written with
// Utf8JsonWriter rather than reflection-based serialization so the shape is
// explicit and stable for consumers.
public static class JsonOutputFormatter
{
    public static string FormatComparison(
        string oldPath, string newPath, SourceLanguage language, FileComparisonResult comparison)
    {
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("old", oldPath);
            writer.WriteString("new", newPath);
            writer.WriteString("language", SourceLanguageDetector.GetDisplayName(language));

            writer.WriteStartObject("summary");
            writer.WriteNumber("added", comparison.Changes.Count(change => change.ChangeType == ChangeType.Added));
            writer.WriteNumber("removed", comparison.Changes.Count(change => change.ChangeType == ChangeType.Removed));
            writer.WriteNumber("edited", comparison.Changes.Count(change => change.ChangeType == ChangeType.Edited));
            writer.WriteNumber("unchanged", comparison.Changes.Count(change => change.ChangeType == ChangeType.Unchanged));
            writer.WriteNumber("movedBlocks", comparison.MovedBlocks.Length);
            writer.WriteEndObject();

            writer.WriteStartArray("movedBlocks");
            foreach (var block in comparison.MovedBlocks)
            {
                writer.WriteStartObject();
                writer.WriteString("type", block.MatchType.ToString());
                writer.WriteString("confidence", block.Confidence.ToString());
                writer.WriteNumber("score", Math.Round(block.Score, 4));
                writer.WriteNumber("oldStart", block.OldStart + 1);
                writer.WriteNumber("oldEnd", block.OldEnd + 1);
                writer.WriteNumber("newStart", block.NewStart + 1);
                writer.WriteNumber("newEnd", block.NewEnd + 1);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("changes");
            foreach (var change in comparison.Changes)
            {
                if (change.ChangeType == ChangeType.Unchanged) continue;

                writer.WriteStartObject();
                writer.WriteString("type", change.ChangeType.ToString());
                WriteNullableNumber(writer, "oldLine", change.OldIndex + 1);
                WriteNullableNumber(writer, "newLine", change.NewIndex + 1);
                if (change.OldLine is not null) writer.WriteString("oldText", change.OldLine);
                if (change.NewLine is not null) writer.WriteString("newText", change.NewLine);
                if (change.TokenChanges is { Length: > 0 } tokens)
                {
                    writer.WriteStartArray("tokens");
                    foreach (var token in tokens)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("type", token.ChangeType.ToString());
                        if (token.OldToken is not null) writer.WriteString("old", token.OldToken);
                        if (token.NewToken is not null) writer.WriteString("new", token.NewToken);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    public static string FormatFolderComparison(
        string basePath, string targetPath, IReadOnlyList<FolderEntryChange> changes)
    {
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("base", basePath);
            writer.WriteString("target", targetPath);
            writer.WriteStartArray("entries");
            foreach (var change in changes)
            {
                writer.WriteStartObject();
                writer.WriteString("path", change.RelativePath);
                writer.WriteString("type", change.ChangeType.ToString());
                if (change.MovedFrom is not null) writer.WriteString("movedFrom", change.MovedFrom);
                if (change.CopiedFrom is not null) writer.WriteString("copiedFrom", change.CopiedFrom);
                if (change.Size is { } size) writer.WriteNumber("size", size);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    public static string FormatThreeWay(IReadOnlyList<ThreeWayChange> changes, IReadOnlyList<string> mergedLines)
    {
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("conflicts", changes.Count(change => change.ChangeType == ChangeType.Conflict));
            writer.WriteStartArray("changes");
            foreach (var change in changes)
            {
                if (change.ChangeType == ChangeType.Unchanged) continue;

                writer.WriteStartObject();
                writer.WriteString("type", change.ChangeType.ToString());
                writer.WriteString("side", change.Side.ToString());
                if (change.BaseLine is not null) writer.WriteString("base", change.BaseLine);
                if (change.LocalLine is not null) writer.WriteString("local", change.LocalLine);
                if (change.RemoteLine is not null) writer.WriteString("remote", change.RemoteLine);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("merged");
            foreach (var line in mergedLines) writer.WriteStringValue(line);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is { } number) writer.WriteNumber(name, number);
        else writer.WriteNull(name);
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            body(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
