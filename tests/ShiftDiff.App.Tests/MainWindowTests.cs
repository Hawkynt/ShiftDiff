using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ShiftDiff.Ui;

namespace ShiftDiff.App.Tests;

// The window is deliberately thin, but XAML only fails at runtime — these tests
// exercise the templates, bindings and wiring on the headless platform.
public class MainWindowTests : IDisposable {
  private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-app", Guid.NewGuid().ToString("N"));

  public MainWindowTests() => Directory.CreateDirectory(_root);

  public void Dispose() {
    if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
  }

  [AvaloniaFact]
  public void Window_Opens_WithEveryNamedRegionPresent() {
    var window = Show();

    Assert.NotNull(window.FindControl<ListBox>("DiffList"));
    Assert.NotNull(window.FindControl<ListBox>("FileList"));
    Assert.NotNull(window.FindControl<ListBox>("MovedBlocksList"));
    Assert.NotNull(window.FindControl<TextBlock>("StatusText"));
    Assert.NotNull(window.FindControl<Border>("InspectorPanel"));
  }

  [AvaloniaFact]
  public void Window_WithTwoFileArguments_RendersDiffRows() {
    var oldPath = Write("old.cs", "int a = 1;\nint b = 2;\n");
    var newPath = Write("new.cs", "int a = 1;\nint b = 3;\n");

    var window = Show(oldPath, newPath);
    PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

    var list = window.FindControl<ListBox>("DiffList")!;
    Assert.True(list.ItemCount > 0);
    Assert.Contains(list.Items.OfType<DiffRow>(), row => row.IsEdited);
  }

  [AvaloniaFact]
  public void Window_AfterComparison_ShowsTheSummaryAndLanguage() {
    var oldPath = Write("old.cs", "int a = 1;\n");
    var newPath = Write("new.cs", "int a = 2;\n");

    var window = Show(oldPath, newPath);
    PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

    Assert.Contains("C#", window.FindControl<TextBlock>("LanguageText")!.Text ?? string.Empty);
    Assert.Contains("edited", window.FindControl<TextBlock>("SummaryText")!.Text ?? string.Empty);
  }

  [AvaloniaFact]
  public void NextChange_SelectsTheFirstChangedRow() {
    var oldPath = Write("old.txt", "a\nb\nc\n");
    var newPath = Write("new.txt", "a\nB\nc\n");

    var window = Show(oldPath, newPath);
    PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

    window.NextChangeCommand.Execute(null);
    Pump();

    var list = window.FindControl<ListBox>("DiffList")!;
    Assert.True(list.SelectedIndex >= 0);
    Assert.True(((DiffRow)list.SelectedItem!).IsChanged);
  }

  [AvaloniaFact]
  public void SelectingAChangedRow_FillsTheInspector() {
    var oldPath = Write("old.txt", "value = 1\n");
    var newPath = Write("new.txt", "value = 2\n");

    var window = Show(oldPath, newPath);
    PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

    window.NextChangeCommand.Execute(null);
    Pump();

    Assert.Equal("edited", window.FindControl<TextBlock>("DetailTitleText")!.Text);
  }

  [AvaloniaFact]
  public void EmojiToggle_SwitchesTheMarkerVocabulary() {
    var window = Show();

    var check = window.FindControl<CheckBox>("EmojiCheck")!;
    check.IsChecked = false;
    Pump();

    Assert.False(window.UseEmojiMarkers);
  }

  [AvaloniaFact]
  public void SidebarAndInspectorToggles_HideTheirPanels() {
    var window = Show();

    window.FindControl<CheckBox>("SidebarCheck")!.IsChecked = false;
    window.FindControl<CheckBox>("InspectorCheck")!.IsChecked = false;
    Pump();

    Assert.False(window.FindControl<Border>("SidebarPanel")!.IsVisible);
    Assert.False(window.FindControl<Border>("InspectorPanel")!.IsVisible);
  }

  [AvaloniaFact]
  public void FolderComparison_PopulatesTheFileListSidebar() {
    var left = Directory.CreateDirectory(Path.Combine(_root, "left")).FullName;
    var right = Directory.CreateDirectory(Path.Combine(_root, "right")).FullName;
    File.WriteAllText(Path.Combine(left, "a.txt"), "one\n");
    File.WriteAllText(Path.Combine(right, "a.txt"), "two\n");

    var window = Show(left, right);
    PumpUntil(() => window.FindControl<ListBox>("FileList")!.ItemCount > 0);

    Assert.Equal(1, window.FindControl<ListBox>("FileList")!.ItemCount);
  }

  [AvaloniaFact]
  public void ThreeDroppedFiles_SwitchTheWindowToThreePanes() {
    var basePath = Write("base.txt", "one\n");
    var localPath = Write("local.txt", "ONE\n");
    var remotePath = Write("remote.txt", "one\n");

    var window = Show(basePath, localPath, remotePath);
    PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

    var headers = window.FindControl<ItemsControl>("PaneHeaders")!;
    Assert.Equal(3, headers.ItemCount);
  }

  [AvaloniaFact]
  public void RenderedFrame_IsProducedWithoutBindingFailures() {
    var oldPath = Write("old.cs", "public int Value => 1;\n");
    var newPath = Write("new.cs", "public int Value => 2;\n");

    var window = Show(oldPath, newPath);
    PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

    using var frame = window.CaptureRenderedFrame();

    Assert.NotNull(frame);
    Assert.True(frame!.Size.Width > 100);
  }

  // AC-009: the window has to come up on a large file pair without freezing.
  [AvaloniaFact]
  public void LargeFilePair_RendersWithoutBlockingTheWindow() {
    var body = string.Join('\n', Enumerable.Range(0, 20_000).Select(i => $"line {i} of the generated file"));
    var oldPath = Write("big.old.txt", body);
    var newPath = Write("big.new.txt", body.Replace("line 10000 of", "CHANGED of"));

    var window = Show(oldPath, newPath);
    PumpUntil(() => window.FindControl<ListBox>("DiffList")!.ItemCount > 0);

    var list = window.FindControl<ListBox>("DiffList")!;
    Assert.True(list.ItemCount > 0);

    // Folding keeps the row count near the change, not near the file size.
    Assert.True(list.ItemCount < 100, $"{list.ItemCount} rows were materialised");
    Assert.Contains("1 edited", window.FindControl<TextBlock>("SummaryText")!.Text ?? string.Empty);
  }

  private MainWindow Show(params string[] args) {
    var window = new MainWindow(args);
    window.Show();
    Pump();
    return window;
  }

  private static void Pump() {
    Dispatcher.UIThread.RunJobs();
    for (var i = 0; i < 4; i++) {
      Thread.Sleep(5);
      Dispatcher.UIThread.RunJobs();
    }
  }

  private static void PumpUntil(Func<bool> condition) {
    for (var i = 0; i < 200 && !condition(); i++) {
      Thread.Sleep(5);
      Dispatcher.UIThread.RunJobs();
    }
  }

  private string Write(string name, string content) {
    var path = Path.Combine(_root, name);
    File.WriteAllText(path, content);
    return path;
  }
}
