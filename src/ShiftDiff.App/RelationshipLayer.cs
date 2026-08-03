using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ShiftDiff.App;

public sealed record VisualRelationship(
    int SourcePane,
    int TargetPane,
    double SourcePosition,
    double TargetPosition,
    string Kind);

public sealed class RelationshipLayer : Control
{
    public IReadOnlyList<VisualRelationship> Links { get; set; } = [];
    public int PaneCount { get; set; } = 2;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (PaneCount < 2 || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var paneWidth = Bounds.Width / PaneCount;
        foreach (var link in Links)
        {
            if (link.SourcePane < 0 || link.TargetPane >= PaneCount || link.SourcePane >= link.TargetPane) continue;

            var color = link.Kind switch
            {
                "folder" => Color.Parse("#9B7BEA"),
                "edited" => Color.Parse("#E0B55B"),
                "block" => Color.Parse("#6D9EEB"),
                _ => Color.Parse("#68B984"),
            };
            var translucent = Color.FromArgb(184, color.R, color.G, color.B);
            var pen = new Pen(new SolidColorBrush(translucent), link.Kind == "folder" ? 2.25 : 1.35);
            var source = new Point((link.SourcePane + 1) * paneWidth - 5, ClampPosition(link.SourcePosition) * Bounds.Height);
            var target = new Point(link.TargetPane * paneWidth + 5, ClampPosition(link.TargetPosition) * Bounds.Height);

            // Araxis-style relationship threads favor a direct visual trajectory.
            // Short horizontal caps keep endpoints legible without covering text.
            var sourceCap = new Point(source.X + 7, source.Y);
            var targetCap = new Point(target.X - 7, target.Y);
            context.DrawLine(pen, source, sourceCap);
            context.DrawLine(pen, sourceCap, targetCap);
            context.DrawLine(pen, targetCap, target);
        }
    }

    private static double ClampPosition(double position) => Math.Clamp(position, 0.015, 0.985);
}
