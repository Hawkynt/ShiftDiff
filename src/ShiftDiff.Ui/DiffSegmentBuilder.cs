using ShiftDiff.Core;

namespace ShiftDiff.Ui;

// Turns one line plus its token-level diff into the runs a pane draws:
// character ranges carrying both "what changed here" and "what syntax class is
// this" (FR-060 inline token diff + syntax highlighting).
public static class DiffSegmentBuilder
{
    public static IReadOnlyList<DiffSegment> Build(
        string? line,
        IReadOnlyList<TokenChange>? tokenChanges,
        bool oldSide,
        SourceLanguage language = SourceLanguage.PlainText,
        DiffSegmentKind fallback = DiffSegmentKind.Unchanged)
    {
        if (string.IsNullOrEmpty(line)) return [];

        var kinds = BuildKindMap(line, tokenChanges, oldSide, fallback);
        var segments = new List<DiffSegment>();

        foreach (var token in Tokenize(line, language))
        {
            var start = token.Start;
            var end = Math.Min(line.Length, start + token.Text.Length);
            if (start >= end) continue;

            var runStart = start;
            for (var i = start + 1; i <= end; i++)
            {
                if (i < end && kinds[i] == kinds[runStart]) continue;

                segments.Add(new DiffSegment(line[runStart..i], kinds[runStart], token.Kind));
                runStart = i;
            }
        }

        return Merge(segments);
    }

    private static DiffSegmentKind[] BuildKindMap(
        string line, IReadOnlyList<TokenChange>? tokenChanges, bool oldSide, DiffSegmentKind fallback)
    {
        var kinds = new DiffSegmentKind[line.Length];
        Array.Fill(kinds, fallback);
        if (tokenChanges is not { Count: > 0 }) return kinds;

        var offset = 0;
        foreach (var change in tokenChanges)
        {
            var text = oldSide ? change.OldToken : change.NewToken;
            if (text is null) continue;

            var kind = change.ChangeType switch
            {
                ChangeType.Added => oldSide ? DiffSegmentKind.Unchanged : DiffSegmentKind.Added,
                ChangeType.Removed => oldSide ? DiffSegmentKind.Removed : DiffSegmentKind.Unchanged,
                ChangeType.Edited => oldSide ? DiffSegmentKind.Removed : DiffSegmentKind.Added,
                _ => DiffSegmentKind.Unchanged,
            };

            var end = Math.Min(line.Length, offset + text.Length);
            for (var i = offset; i < end; i++) kinds[i] = kind;
            offset = end;

            // The token stream no longer lines up with the rendered text (a
            // normalization mode rewrote it); leave the remainder as-is rather
            // than mis-colouring it.
            if (offset >= line.Length) break;
        }

        return kinds;
    }

    private static IReadOnlyList<SourceToken> Tokenize(string line, SourceLanguage language)
    {
        if (language == SourceLanguage.PlainText) return [new SourceToken(SourceTokenKind.Identifier, line, 0)];

        try
        {
            return SourceTokenizer.TokenizeLine(line, language);
        }
        catch (ArgumentException)
        {
            return [new SourceToken(SourceTokenKind.Identifier, line, 0)];
        }
    }

    private static IReadOnlyList<DiffSegment> Merge(List<DiffSegment> segments)
    {
        if (segments.Count <= 1) return segments;

        var merged = new List<DiffSegment>(segments.Count) { segments[0] };
        foreach (var segment in segments.Skip(1))
        {
            var previous = merged[^1];
            if (previous.Kind == segment.Kind && previous.Syntax == segment.Syntax)
            {
                merged[^1] = previous with { Text = previous.Text + segment.Text };
                continue;
            }

            merged.Add(segment);
        }

        return merged;
    }
}
