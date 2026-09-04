using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.App.Controls;

// FR-042 compact overview/minimap: the whole file's change density in one
// column, clickable to jump. Drawn directly rather than composed from controls
// so a 100k-line document costs nothing to render.
public sealed class OverviewBar : Control {
  public static readonly StyledProperty<IEnumerable<OverviewStripe>?> StripesProperty =
      AvaloniaProperty.Register<OverviewBar, IEnumerable<OverviewStripe>?>(nameof(Stripes));

  public static readonly StyledProperty<double> ViewportStartProperty =
      AvaloniaProperty.Register<OverviewBar, double>(nameof(ViewportStart));

  public static readonly StyledProperty<double> ViewportEndProperty =
      AvaloniaProperty.Register<OverviewBar, double>(nameof(ViewportEnd), 1.0);

  public static readonly StyledProperty<double> CursorPositionProperty =
      AvaloniaProperty.Register<OverviewBar, double>(nameof(CursorPosition), -1);

  static OverviewBar() {
    AffectsRender<OverviewBar>(StripesProperty, ViewportStartProperty, ViewportEndProperty, CursorPositionProperty);
  }

  /// <summary>Raised with a normalized 0..1 document position when the user clicks or drags.</summary>
  public event EventHandler<double>? PositionPicked;

  public IEnumerable<OverviewStripe>? Stripes {
    get => GetValue(StripesProperty);
    set => SetValue(StripesProperty, value);
  }

  public double ViewportStart {
    get => GetValue(ViewportStartProperty);
    set => SetValue(ViewportStartProperty, value);
  }

  public double ViewportEnd {
    get => GetValue(ViewportEndProperty);
    set => SetValue(ViewportEndProperty, value);
  }

  public double CursorPosition {
    get => GetValue(CursorPositionProperty);
    set => SetValue(CursorPositionProperty, value);
  }

  public override void Render(DrawingContext context) {
    var width = Bounds.Width;
    var height = Bounds.Height;
    if (width <= 0 || height <= 0) return;

    context.FillRectangle(Brush("GutterBrush", Colors.Black), new Rect(0, 0, width, height));

    foreach (var stripe in Stripes ?? []) {
      var top = stripe.Start * height;
      var stripeHeight = Math.Max(2, (stripe.End - stripe.Start) * height);
      context.FillRectangle(BrushFor(stripe.ChangeType), new Rect(2, top, width - 4, stripeHeight));
    }

    if (CursorPosition >= 0) {
      var y = CursorPosition * height;
      context.FillRectangle(Brush("AccentBrush", Colors.MediumPurple), new Rect(0, Math.Max(0, y - 1), width, 2));
    }

    var viewportTop = Math.Clamp(ViewportStart, 0, 1) * height;
    var viewportBottom = Math.Clamp(ViewportEnd, 0, 1) * height;
    if (viewportBottom - viewportTop < height - 1) {
      var pen = new Pen(Brush("BorderBrushColor", Colors.Gray), 1);
      context.DrawRectangle(null, pen, new Rect(0.5, viewportTop, width - 1, Math.Max(4, viewportBottom - viewportTop)));
    }
  }

  protected override void OnPointerPressed(PointerPressedEventArgs e) {
    base.OnPointerPressed(e);
    Pick(e.GetPosition(this).Y);
    e.Pointer.Capture(this);
  }

  protected override void OnPointerMoved(PointerEventArgs e) {
    base.OnPointerMoved(e);
    if (Equals(e.Pointer.Captured, this)) Pick(e.GetPosition(this).Y);
  }

  protected override void OnPointerReleased(PointerReleasedEventArgs e) {
    base.OnPointerReleased(e);
    e.Pointer.Capture(null);
  }

  private void Pick(double y) {
    if (Bounds.Height <= 0) return;
    PositionPicked?.Invoke(this, Math.Clamp(y / Bounds.Height, 0, 1));
  }

  private IBrush BrushFor(ChangeType changeType) => changeType switch {
    ChangeType.Added => Brush("AddedAccentBrush", Colors.MediumSeaGreen),
    ChangeType.Removed => Brush("RemovedAccentBrush", Colors.IndianRed),
    ChangeType.Edited => Brush("EditedAccentBrush", Colors.Goldenrod),
    ChangeType.Moved or ChangeType.MovedEdited => Brush("MovedAccentBrush", Colors.MediumPurple),
    ChangeType.Conflict => Brush("ConflictAccentBrush", Colors.OrangeRed),
    _ => Brushes.Transparent,
  };

  private IBrush Brush(string resourceKey, Color fallback) =>
      this.TryFindResource(resourceKey, ActualThemeVariant, out var resource) && resource is IBrush brush
          ? brush
          : new SolidColorBrush(fallback);
}
