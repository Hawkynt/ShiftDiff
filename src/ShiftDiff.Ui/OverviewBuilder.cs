using ShiftDiff.Core;

namespace ShiftDiff.Ui;

// FR-042 compact overview/minimap: the whole document compressed into stripes
// positioned in normalized 0..1 space, so the bar renders at any height without
// knowing the row height.
public static class OverviewBuilder
{
    public static IReadOnlyList<OverviewStripe> Build(IReadOnlyList<DiffRow> rows)
    {
        if (rows.Count == 0) return [];

        var stripes = new List<OverviewStripe>();
        var height = 1.0 / rows.Count;

        var runStart = -1;
        ChangeType runType = ChangeType.Unchanged;

        for (var i = 0; i < rows.Count; i++)
        {
            var type = rows[i].IsChanged ? rows[i].DisplayChangeType : ChangeType.Unchanged;
            if (type == ChangeType.Unchanged)
            {
                Flush(i);
                continue;
            }

            if (runStart >= 0 && type == runType) continue;

            Flush(i);
            runStart = i;
            runType = type;
        }

        Flush(rows.Count);
        return stripes;

        void Flush(int end)
        {
            if (runStart < 0) return;

            stripes.Add(new OverviewStripe(runStart * height, end * height, runType, runStart));
            runStart = -1;
        }
    }
}
