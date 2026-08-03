namespace ShiftDiff.Core;

public sealed record MergeSourceBlock(
    string SourceId,
    string SourcePath,
    int StartLine,
    int EndLine,
    IReadOnlyList<string> Lines,
    ChangeType ChangeType = ChangeType.Edited,
    Confidence Confidence = Confidence.Certain)
{
    public int LineCount => Lines.Count;
}

public enum MergeEditKind { Insert, Replace }

public sealed record MergeEdit(
    MergeEditKind Kind,
    MergeSourceBlock Block,
    int TargetStart,
    int ReplacedLineCount);

public sealed class InteractiveMergeDocument
{
    private readonly List<string> _lines;
    private readonly Stack<(string[] Lines, MergeEdit Edit)> _history = new();

    public InteractiveMergeDocument(IEnumerable<string> initialLines)
    {
        ArgumentNullException.ThrowIfNull(initialLines);
        _lines = initialLines.ToList();
    }

    public IReadOnlyList<string> Lines => _lines;
    public IReadOnlyCollection<MergeEdit> History => _history.Select(item => item.Edit).ToArray();
    public bool CanUndo => _history.Count > 0;

    public MergeEdit Insert(MergeSourceBlock block, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (targetIndex < 0 || targetIndex > _lines.Count) throw new ArgumentOutOfRangeException(nameof(targetIndex));

        var edit = new MergeEdit(MergeEditKind.Insert, block, targetIndex, 0);
        Remember(edit);
        _lines.InsertRange(targetIndex, block.Lines);
        return edit;
    }

    public MergeEdit Replace(MergeSourceBlock block, int targetStart, int targetLineCount)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (targetStart < 0 || targetStart > _lines.Count) throw new ArgumentOutOfRangeException(nameof(targetStart));
        if (targetLineCount < 0 || targetStart + targetLineCount > _lines.Count) throw new ArgumentOutOfRangeException(nameof(targetLineCount));

        var edit = new MergeEdit(MergeEditKind.Replace, block, targetStart, targetLineCount);
        Remember(edit);
        _lines.RemoveRange(targetStart, targetLineCount);
        _lines.InsertRange(targetStart, block.Lines);
        return edit;
    }

    public bool Undo()
    {
        if (!_history.TryPop(out var snapshot)) return false;
        _lines.Clear();
        _lines.AddRange(snapshot.Lines);
        return true;
    }

    private void Remember(MergeEdit edit) => _history.Push((_lines.ToArray(), edit));
}

