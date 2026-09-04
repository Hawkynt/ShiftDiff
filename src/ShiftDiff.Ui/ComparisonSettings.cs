using ShiftDiff.Core;

namespace ShiftDiff.Ui;

public enum ThemeMode {
  Dark,
  Light,
  System,
}

public enum PaneLayout {
  SideBySide,
  Unified,
  ThreeWay,
  FourWay,
}

// SPEC section 11 Configuration, as far as it affects a comparison and its
// rendering. Everything here is observable so the shell can recompare or
// re-render as soon as one value changes.
public sealed class ComparisonSettings : ObservableObject {
  private bool _ignoreCase;
  private WhitespaceMode _whitespace = WhitespaceMode.None;
  private DetectionMode _detection = DetectionMode.Balanced;
  private int _contextLines = 3;
  private bool _collapseUnchanged = true;
  private bool _showEmojiMarkers = true;
  private bool _syntaxHighlighting = true;
  private bool _wordWrap;
  private bool _highContrast;
  private ThemeMode _theme = ThemeMode.Dark;
  private PaneLayout _layout = PaneLayout.SideBySide;
  private double _fontSize = 13;

  /// <summary>Raised when a setting changes that requires re-running the comparison.</summary>
  public event EventHandler? ComparisonAffectingChanged;

  /// <summary>Raised when a setting changes that only affects rendering.</summary>
  public event EventHandler? PresentationChanged;

  public bool IgnoreCase {
    get => _ignoreCase;
    set => SetComparison(ref _ignoreCase, value);
  }

  public WhitespaceMode Whitespace {
    get => _whitespace;
    set => SetComparison(ref _whitespace, value);
  }

  public DetectionMode Detection {
    get => _detection;
    set => SetComparison(ref _detection, value);
  }

  public int ContextLines {
    get => _contextLines;
    set => SetPresentation(ref _contextLines, Math.Max(0, value));
  }

  public bool CollapseUnchanged {
    get => _collapseUnchanged;
    set => SetPresentation(ref _collapseUnchanged, value);
  }

  // FR-043: emoji markers must be disableable.
  public bool ShowEmojiMarkers {
    get => _showEmojiMarkers;
    set => SetPresentation(ref _showEmojiMarkers, value);
  }

  public bool SyntaxHighlighting {
    get => _syntaxHighlighting;
    set => SetPresentation(ref _syntaxHighlighting, value);
  }

  public bool WordWrap {
    get => _wordWrap;
    set => SetPresentation(ref _wordWrap, value);
  }

  // FR-044: colourblind-safe / high contrast presentation.
  public bool HighContrast {
    get => _highContrast;
    set => SetPresentation(ref _highContrast, value);
  }

  public ThemeMode Theme {
    get => _theme;
    set => SetPresentation(ref _theme, value);
  }

  public PaneLayout Layout {
    get => _layout;
    set => SetPresentation(ref _layout, value);
  }

  public double FontSize {
    get => _fontSize;
    set => SetPresentation(ref _fontSize, Math.Clamp(value, 8, 32));
  }

  private void SetComparison<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null) {
    if (!SetProperty(ref field, value, name)) return;
    ComparisonAffectingChanged?.Invoke(this, EventArgs.Empty);
  }

  private void SetPresentation<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null) {
    if (!SetProperty(ref field, value, name)) return;
    PresentationChanged?.Invoke(this, EventArgs.Empty);
  }
}
