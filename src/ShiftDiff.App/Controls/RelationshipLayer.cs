using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ShiftDiff.Core;

namespace ShiftDiff.App.Controls;

/// <summary>One ribbon to draw, in normalized 0..1 viewport coordinates.</summary>
public sealed record VisualRelationship(
    int SourcePane,
    int TargetPane,
    double SourceTop,
    double SourceBottom,
    double TargetTop,
    double TargetBottom,
    ChangeType Kind,
    bool IsRelocation);

// Araxis-style linking: the gutter between two panes carries one connector per
// change block, drawn as a bracket around the block on each side joined by a
// line — "… text ]————[ text …". An aligned block gives a level connector, an
// insertion or deletion a bracket collapsed to a tick, and a relocated block a
// sloped line between the place it left and where it arrived. Drawn directly
// rather than composed from controls so a large document costs nothing.
public sealed class RelationshipLayer : Control
{
    public static readonly StyledProperty<int> PaneCountProperty =
        AvaloniaProperty.Register<RelationshipLayer, int>(nameof(PaneCount), 2);

    public static readonly StyledProperty<double> GutterWidthProperty =
        AvaloniaProperty.Register<RelationshipLayer, double>(nameof(GutterWidth), 38);

    // How far the bracket spine stands off the block outline, and how far the
    // serifs reach back towards the text.
    private const double SpineInset = 13;
    private const double SerifLength = 8;

    private IReadOnlyList<VisualRelationship> _links = [];
    private IReadOnlyList<double> _paneRightEdges = [];

    static RelationshipLayer() => AffectsRender<RelationshipLayer>(PaneCountProperty, GutterWidthProperty);

    public IReadOnlyList<VisualRelationship> Links
    {
        get => _links;
        set
        {
            _links = value;
            InvalidateVisual();
        }
    }

    public int PaneCount
    {
        get => GetValue(PaneCountProperty);
        set => SetValue(PaneCountProperty, value);
    }

    /// <summary>
    /// The x of each pane's right edge, measured from the laid-out rows. Rows can
    /// be wider than the viewport (they scroll horizontally), so the boundaries
    /// cannot be derived by dividing the visible width.
    /// </summary>
    public IReadOnlyList<double> PaneRightEdges
    {
        get => _paneRightEdges;
        set
        {
            _paneRightEdges = value;
            InvalidateVisual();
        }
    }

    /// <summary>Width of the gutter each pane reserves on its right for the ribbons.</summary>
    public double GutterWidth
    {
        get => GetValue(GutterWidthProperty);
        set => SetValue(GutterWidthProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (PaneCount < 2 || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var height = Bounds.Height;

        foreach (var link in Links)
        {
            if (link.SourcePane < 0 || link.TargetPane >= PaneCount || link.SourcePane >= link.TargetPane) continue;

            // The gutter sits at the right edge of the source pane.
            var right = RightEdgeOf(link.SourcePane);
            var left = right - GutterWidth;
            if (right <= 0) continue;

            var sourceTop = Clamp(link.SourceTop) * height;
            var sourceBottom = Clamp(link.SourceBottom) * height;
            var targetTop = Clamp(link.TargetTop) * height;
            var targetBottom = Clamp(link.TargetBottom) * height;

            var colour = ColourFor(link.Kind);
            var pen = new Pen(
                new SolidColorBrush(Color.FromArgb(235, colour.R, colour.G, colour.B)),
                link.IsRelocation ? 1.8 : 1.4,
                lineCap: PenLineCap.Square);

            // The block is bracketed on each side and the brackets are joined:
            //   … text ]————[ text …
            // The spines stand clear of the block outline, otherwise that outline
            // reads as the spine and the serifs appear to point the wrong way.
            var leftSpine = left + SpineInset;
            var rightSpine = right - SpineInset;

            DrawBracket(context, pen, leftSpine, sourceTop, sourceBottom, serifDirection: -1);
            DrawBracket(context, pen, rightSpine, targetTop, targetBottom, serifDirection: 1);

            context.DrawLine(
                pen,
                new Point(leftSpine, (sourceTop + sourceBottom) / 2),
                new Point(rightSpine, (targetTop + targetBottom) / 2));
        }
    }

    // One half of the "]————[" pair: a spine spanning the block with serifs at
    // both ends pointing back at the text it belongs to — serifDirection -1 for
    // the "]" on a left-hand pane, +1 for the "[" on a right-hand one. A block
    // that contributes no lines on this side collapses to a single tick.
    private static void DrawBracket(
        DrawingContext context, Pen pen, double spine, double top, double bottom, int serifDirection)
    {
        if (bottom - top > 1.5) context.DrawLine(pen, new Point(spine, top), new Point(spine, bottom));
        else
        {
            var middle = (top + bottom) / 2;
            top = middle;
            bottom = middle;
        }

        var serifEnd = spine + serifDirection * SerifLength;
        context.DrawLine(pen, new Point(spine, top), new Point(serifEnd, top));
        context.DrawLine(pen, new Point(spine, bottom), new Point(serifEnd, bottom));
    }

    private double RightEdgeOf(int pane) =>
        pane < PaneRightEdges.Count ? PaneRightEdges[pane] : (pane + 1) * (Bounds.Width / Math.Max(1, PaneCount));

    private static double Clamp(double position) => Math.Clamp(position, -0.2, 1.2);

    private Color ColourFor(ChangeType changeType) => changeType switch
    {
        ChangeType.Added => Resource("AddedAccentBrush", Colors.MediumSeaGreen),
        ChangeType.Removed => Resource("RemovedAccentBrush", Colors.IndianRed),
        ChangeType.Edited => Resource("EditedAccentBrush", Colors.Goldenrod),
        ChangeType.Moved or ChangeType.MovedEdited => Resource("MovedAccentBrush", Colors.MediumPurple),
        ChangeType.Conflict => Resource("ConflictAccentBrush", Colors.OrangeRed),
        _ => Resource("BorderBrushColor", Colors.Gray),
    };

    private Color Resource(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is ISolidColorBrush brush
            ? brush.Color
            : fallback;
}
