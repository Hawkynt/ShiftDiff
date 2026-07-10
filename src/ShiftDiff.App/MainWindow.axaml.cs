using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using ShiftDiff.Core;

namespace ShiftDiff.App;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<DiffRowViewModel> _rows = [];
    private readonly ObservableCollection<MovedBlockViewModel> _movedBlocks = [];
    private string? _oldPath;
    private string? _newPath;
    private int _currentChange = -1;

    public MainWindow() : this([]) { }

    public MainWindow(IReadOnlyList<string> args)
    {
        InitializeComponent();

        DiffListControl.ItemsSource = _rows;
        MovedBlocksListControl.ItemsSource = _movedBlocks;
        WhitespaceSelectorControl.ItemsSource = Enum.GetValues<WhitespaceMode>();
        WhitespaceSelectorControl.SelectedItem = WhitespaceMode.None;
        DetectionSelectorControl.ItemsSource = Enum.GetValues<DetectionMode>();
        DetectionSelectorControl.SelectedItem = DetectionMode.Balanced;
        ThemeSelectorControl.ItemsSource = new[] { "Dark", "Light", "System" };
        ThemeSelectorControl.SelectedIndex = 0;

        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);

        if (args.Count >= 2)
        {
            _oldPath = args[0];
            _newPath = args[1];
            Opened += async (_, _) => await CompareAsync();
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private TextBlock OldPathLabel => this.FindControl<TextBlock>("OldPathText")!;
    private TextBlock NewPathLabel => this.FindControl<TextBlock>("NewPathText")!;
    private TextBlock LanguageLabel => this.FindControl<TextBlock>("LanguageText")!;
    private TextBlock SummaryLabel => this.FindControl<TextBlock>("SummaryText")!;
    private TextBlock StatusLabel => this.FindControl<TextBlock>("StatusText")!;
    private ProgressBar BusyIndicatorControl => this.FindControl<ProgressBar>("BusyIndicator")!;
    private ListBox DiffListControl => this.FindControl<ListBox>("DiffList")!;
    private ListBox MovedBlocksListControl => this.FindControl<ListBox>("MovedBlocksList")!;
    private ComboBox WhitespaceSelectorControl => this.FindControl<ComboBox>("WhitespaceSelector")!;
    private ComboBox DetectionSelectorControl => this.FindControl<ComboBox>("DetectionSelector")!;
    private ComboBox ThemeSelectorControl => this.FindControl<ComboBox>("ThemeSelector")!;
    private CheckBox IgnoreCaseCheckControl => this.FindControl<CheckBox>("IgnoreCaseCheck")!;

    private async void OnOpenOld(object? sender, RoutedEventArgs e)
    {
        if (await PickFileAsync("Open original source") is { } path)
        {
            _oldPath = path;
            await CompareWhenReadyAsync();
        }
    }

    private async void OnOpenNew(object? sender, RoutedEventArgs e)
    {
        if (await PickFileAsync("Open changed source") is { } path)
        {
            _newPath = path;
            await CompareWhenReadyAsync();
        }
    }

    private async void OnCompare(object? sender, RoutedEventArgs e) => await CompareAsync();

    private async void OnSwap(object? sender, RoutedEventArgs e)
    {
        (_oldPath, _newPath) = (_newPath, _oldPath);
        await CompareWhenReadyAsync();
    }

    private void OnPreviousChange(object? sender, RoutedEventArgs e) => NavigateChange(-1);

    private void OnNextChange(object? sender, RoutedEventArgs e) => NavigateChange(1);

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Application.Current is null || ThemeSelectorControl.SelectedItem is not string theme) return;
        Application.Current.RequestedThemeVariant = theme switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.GetFiles()?
            .OfType<IStorageFile>()
            .Select(file => file.Path.LocalPath)
            .Where(File.Exists)
            .Take(2)
            .ToArray();

        if (paths is not { Length: > 0 }) return;

        if (paths.Length == 2)
        {
            _oldPath = paths[0];
            _newPath = paths[1];
        }
        else if (_oldPath is null)
        {
            _oldPath = paths[0];
        }
        else
        {
            _newPath = paths[0];
        }

        await CompareWhenReadyAsync();
    }

    private async Task<string?> PickFileAsync(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Source and text files") { Patterns = ["*.cs", "*.pl", "*.pm", "*.py", "*.php", "*.go", "*.rs", "*.c", "*.h", "*.cpp", "*.hpp", "*.vb", "*.vbs", "*.rb", "*.js", "*.ts", "*.java", "*.html", "*.css", "*.sql", "*.txt"] },
                FilePickerFileTypes.All,
            ],
        });

        return files.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task CompareWhenReadyAsync()
    {
        UpdatePaths();
        if (_oldPath is not null && _newPath is not null) await CompareAsync();
    }

    private async Task CompareAsync()
    {
        UpdatePaths();
        if (_oldPath is null || _newPath is null)
        {
            StatusLabel.Text = "Choose an old and a new file first.";
            return;
        }

        try
        {
            SetBusy(true, "Reading and analysing source files…");
            var oldBytesTask = File.ReadAllBytesAsync(_oldPath);
            var newBytesTask = File.ReadAllBytesAsync(_newPath);
            await Task.WhenAll(oldBytesTask, newBytesTask);

            var oldPath = _oldPath;
            var newPath = _newPath;
            var whitespace = WhitespaceSelectorControl.SelectedItem is WhitespaceMode selectedWhitespace ? selectedWhitespace : WhitespaceMode.None;
            var detection = DetectionSelectorControl.SelectedItem is DetectionMode selectedDetection ? selectedDetection : DetectionMode.Balanced;
            var ignoreCase = IgnoreCaseCheckControl.IsChecked == true;

            var result = await Task.Run(() => FileComparer.CompareSourceFiles(
                oldBytesTask.Result,
                newBytesTask.Result,
                oldPath,
                newPath,
                ignoreCase,
                whitespace,
                detection));

            Present(result);
        }
        catch (Exception exception)
        {
            StatusLabel.Text = $"Comparison failed: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Present(SourceFileComparisonResult sourceResult)
    {
        _rows.Clear();
        _movedBlocks.Clear();
        _currentChange = -1;

        var comparison = sourceResult.Comparison;
        foreach (var change in comparison.Changes)
        {
            var moved = comparison.MovedBlocks.Any(block =>
                (change.OldIndex is { } oldIndex && oldIndex >= block.OldStart && oldIndex <= block.OldEnd)
                || (change.NewIndex is { } newIndex && newIndex >= block.NewStart && newIndex <= block.NewEnd));
            _rows.Add(DiffRowViewModel.From(change, moved));
        }

        foreach (var block in comparison.MovedBlocks)
        {
            _movedBlocks.Add(new MovedBlockViewModel(
                $"{block.MatchType} · {block.Confidence}",
                $"old {block.OldStart + 1}–{block.OldEnd + 1} → new {block.NewStart + 1}–{block.NewEnd + 1} · {block.SimilarityScore:P0}"));
        }

        var added = comparison.Changes.Count(change => change.ChangeType == ChangeType.Added);
        var removed = comparison.Changes.Count(change => change.ChangeType == ChangeType.Removed);
        var edited = comparison.Changes.Count(change => change.ChangeType == ChangeType.Edited);
        LanguageLabel.Text = $"Language: {SourceLanguageDetector.GetDisplayName(sourceResult.Language)}";
        SummaryLabel.Text = $"{added} added · {removed} removed · {edited} edited · {comparison.MovedBlocks.Length} moved blocks";
        StatusLabel.Text = $"Compared {Path.GetFileName(_oldPath)} with {Path.GetFileName(_newPath)}";
    }

    private void NavigateChange(int direction)
    {
        if (_rows.Count == 0) return;

        for (var attempts = 0; attempts < _rows.Count; attempts++)
        {
            _currentChange = (_currentChange + direction + _rows.Count) % _rows.Count;
            if (!_rows[_currentChange].IsChanged) continue;
            DiffListControl.SelectedIndex = _currentChange;
            DiffListControl.ScrollIntoView(_rows[_currentChange]);
            return;
        }
    }

    private void UpdatePaths()
    {
        OldPathLabel.Text = _oldPath ?? "Drop or open the original file";
        NewPathLabel.Text = _newPath ?? "Drop or open the changed file";
    }

    private void SetBusy(bool isBusy, string? status = null)
    {
        BusyIndicatorControl.IsVisible = isBusy;
        if (status is not null) StatusLabel.Text = status;
    }
}

public sealed record MovedBlockViewModel(string Title, string Detail);

public sealed record DiffRowViewModel(
    string Marker,
    IBrush MarkerForeground,
    IBrush GutterBackground,
    IBrush OldBackground,
    IBrush NewBackground,
    string OldLineNumber,
    string OldLine,
    string NewLineNumber,
    string NewLine,
    bool IsChanged)
{
    private static readonly IBrush Transparent = Brushes.Transparent;
    private static readonly IBrush Gutter = Brush("#181D24");
    private static readonly IBrush Added = Brush("#173527");
    private static readonly IBrush Removed = Brush("#3A2027");
    private static readonly IBrush Edited = Brush("#39331F");
    private static readonly IBrush AddedForeground = Brush("#66C58A");
    private static readonly IBrush RemovedForeground = Brush("#D97882");
    private static readonly IBrush EditedForeground = Brush("#E0C56A");
    private static readonly IBrush MovedForeground = Brush("#B6A8FF");

    public static DiffRowViewModel From(LineChange change, bool moved)
    {
        var (marker, markerForeground, oldBackground, newBackground) = change.ChangeType switch
        {
            ChangeType.Added => ("+", AddedForeground, Transparent, Added),
            ChangeType.Removed => ("−", RemovedForeground, Removed, Transparent),
            ChangeType.Edited => ("~", EditedForeground, Edited, Edited),
            _ when moved => ("M", MovedForeground, Transparent, Transparent),
            _ => ("", Brushes.Transparent, Transparent, Transparent),
        };

        return new DiffRowViewModel(
            marker,
            markerForeground,
            Gutter,
            oldBackground,
            newBackground,
            change.OldIndex is { } oldIndex ? (oldIndex + 1).ToString() : "",
            change.OldLine ?? "",
            change.NewIndex is { } newIndex ? (newIndex + 1).ToString() : "",
            change.NewLine ?? "",
            change.ChangeType != ChangeType.Unchanged || moved);
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
