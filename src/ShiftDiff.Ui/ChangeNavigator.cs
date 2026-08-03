namespace ShiftDiff.Ui;

// FR-045 Navigation: next/previous change, conflict and moved block, plus the
// jump between the two ends of one moved block.
public sealed class ChangeNavigator(DiffDocument document)
{
    public DiffDocument Document { get; } = document;

    public int Next(int currentRow) => Forward(Document.ChangeRowIndices, currentRow);

    public int Previous(int currentRow) => Backward(Document.ChangeRowIndices, currentRow);

    public int NextConflict(int currentRow) => Forward(Document.ConflictRowIndices, currentRow);

    public int PreviousConflict(int currentRow) => Backward(Document.ConflictRowIndices, currentRow);

    public int NextMoved(int currentRow) => Forward(Document.MovedRowIndices, currentRow);

    public int PreviousMoved(int currentRow) => Backward(Document.MovedRowIndices, currentRow);

    public int First => Document.ChangeRowIndices.Count > 0 ? Document.ChangeRowIndices[0] : -1;

    public int Last => Document.ChangeRowIndices.Count > 0 ? Document.ChangeRowIndices[^1] : -1;

    /// <summary>Index of the change the given row belongs to, 1-based; 0 when the row is unchanged.</summary>
    public int OrdinalOf(int row)
    {
        for (var i = Document.ChangeRowIndices.Count - 1; i >= 0; i--)
        {
            if (Document.ChangeRowIndices[i] <= row) return IsInsideRun(row, Document.ChangeRowIndices[i]) ? i + 1 : 0;
        }

        return 0;
    }

    /// <summary>FR-045: from one end of a moved block, jump to the other end.</summary>
    public int PairedRow(int currentRow)
    {
        if (currentRow < 0 || currentRow >= Document.Rows.Count) return -1;

        var row = Document.Rows[currentRow];
        if (row.MovedBlockId is not { } id) return -1;

        var block = Document.MovedBlocks.FirstOrDefault(candidate => candidate.Id == id);
        if (block is null) return -1;

        var atOldEnd = row.OldIndex is { } oldIndex && oldIndex >= block.OldStart && oldIndex <= block.OldEnd;
        var target = atOldEnd ? block.NewRowIndex : block.OldRowIndex;
        return target == currentRow ? (atOldEnd ? block.OldRowIndex : block.NewRowIndex) : target;
    }

    private bool IsInsideRun(int row, int runStart)
    {
        for (var i = runStart; i <= row && i < Document.Rows.Count; i++)
        {
            if (!Document.Rows[i].IsChanged) return false;
        }

        return true;
    }

    private static int Forward(IReadOnlyList<int> indices, int currentRow)
    {
        foreach (var index in indices)
        {
            if (index > currentRow) return index;
        }

        return indices.Count > 0 ? indices[0] : -1;
    }

    private static int Backward(IReadOnlyList<int> indices, int currentRow)
    {
        for (var i = indices.Count - 1; i >= 0; i--)
        {
            if (indices[i] < currentRow) return indices[i];
        }

        return indices.Count > 0 ? indices[^1] : -1;
    }
}
