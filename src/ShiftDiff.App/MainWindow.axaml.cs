using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
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
    private readonly List<LoadedSource> _loadedSources = [];
    private WorkspaceComparison? _workspace;
    private InteractiveMergeDocument? _mergeDocument;
    private bool _suppressFolderSelection;
    private int _currentChange = -1;

    public ObservableCollection<WorkspacePaneViewModel> Panes { get; } = [];
    public ObservableCollection<MergeLineViewModel> MergeLines { get; } = [];

    public MainWindow() : this([]) { }

    public MainWindow(IReadOnlyList<string> args)
    {
        InitializeComponent();
        DataContext = this;

        WhitespaceSelectorControl.ItemsSource = Enum.GetValues<WhitespaceMode>();
        WhitespaceSelectorControl.SelectedItem = WhitespaceMode.None;
        DetectionSelectorControl.ItemsSource = Enum.GetValues<DetectionMode>();
        DetectionSelectorControl.SelectedItem = DetectionMode.Balanced;

        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);

        if (args.Count is >= 2 and <= 4)
        {
            Opened += async (_, _) => await LoadPathsAsync(args);
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private TextBlock FolderSummaryLabel => this.FindControl<TextBlock>("FolderSummaryText")!;
    private TextBlock FileSummaryLabel => this.FindControl<TextBlock>("FileSummaryText")!;
    private TextBlock TargetPathLabel => this.FindControl<TextBlock>("TargetPathText")!;
    private TextBlock MergeStatusLabel => this.FindControl<TextBlock>("MergeStatusText")!;
    private TextBlock StatusLabel => this.FindControl<TextBlock>("StatusText")!;
    private ProgressBar BusyIndicatorControl => this.FindControl<ProgressBar>("BusyIndicator")!;
    private ComboBox WhitespaceSelectorControl => this.FindControl<ComboBox>("WhitespaceSelector")!;
    private ComboBox DetectionSelectorControl => this.FindControl<ComboBox>("DetectionSelector")!;
    private CheckBox IgnoreCaseCheckControl => this.FindControl<CheckBox>("IgnoreCaseCheck")!;
    private ListBox MergeTargetListControl => this.FindControl<ListBox>("MergeTargetList")!;
    private RelationshipLayer FolderLinksControl => this.FindControl<RelationshipLayer>("FolderRelationshipLayer")!;
    private RelationshipLayer BlockLinksControl => this.FindControl<RelationshipLayer>("BlockRelationshipLayer")!;

    private async void OnOpenFiles(object? sender, RoutedEventArgs e)
    {
        var items = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Compare two to four files",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.All],
        });
        await LoadPathsAsync(items.Select(item => item.Path.LocalPath).Take(4).ToArray());
    }

    private async void OnOpenFolders(object? sender, RoutedEventArgs e)
    {
        var items = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Compare two to four folders",
            AllowMultiple = true,
        });
        await LoadPathsAsync(items.Select(item => item.Path.LocalPath).Take(4).ToArray());
    }

    private async void OnRecompare(object? sender, RoutedEventArgs e)
    {
        if (_loadedSources.Count >= 2) await BuildWorkspaceAsync(resetTarget: false);
    }

    private void OnLightTheme(object? sender, RoutedEventArgs e) => SetTheme(ThemeVariant.Light);
    private void OnSystemTheme(object? sender, RoutedEventArgs e) => SetTheme(ThemeVariant.Default);
    private void OnDarkTheme(object? sender, RoutedEventArgs e) => SetTheme(ThemeVariant.Dark);

    private static void SetTheme(ThemeVariant variant)
    {
        if (Application.Current is not null) Application.Current.RequestedThemeVariant = variant;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.TryGetFiles()?
            .Select(item => item.Path.LocalPath)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Take(4)
            .ToArray();
        if (paths is { Length: > 0 }) await LoadPathsAsync(paths);
    }

    private async Task LoadPathsAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count is < 2 or > 4)
        {
            StatusLabel.Text = "Select two to four files or two to four folders.";
            return;
        }

        var areFolders = paths.All(Directory.Exists);
        var areFiles = paths.All(File.Exists);
        if (!areFolders && !areFiles)
        {
            StatusLabel.Text = "A session cannot mix files and folders; the contents may mix freely afterwards.";
            return;
        }

        try
        {
            SetBusy(true, areFolders ? "Reading folder snapshots…" : "Reading source files…");
            _loadedSources.Clear();

            for (var index = 0; index < paths.Count; index++)
            {
                var path = paths[index];
                var files = areFolders
                    ? await ReadFolderAsync(path)
                    : new Dictionary<string, byte[]>(StringComparer.Ordinal)
                    {
                        [Path.GetFileName(path)] = await File.ReadAllBytesAsync(path),
                    };
                var label = areFolders
                    ? new DirectoryInfo(path).Name
                    : Path.GetFileName(path);
                var source = new WorkspaceSource($"pane-{index + 1}", label, files);
                _loadedSources.Add(new LoadedSource(source, path, areFolders));
            }

            await BuildWorkspaceAsync(resetTarget: true);
        }
        catch (Exception exception)
        {
            StatusLabel.Text = $"Unable to open comparison: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static Task<Dictionary<string, byte[]>> ReadFolderAsync(string root) => Task.Run(() =>
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
            files[relativePath] = File.ReadAllBytes(path);
        }

        return files;
    });

    private async Task BuildWorkspaceAsync(bool resetTarget)
    {
        SetBusy(true, "Matching files, folders and moves…");
        try
        {
            var sources = _loadedSources.Select(source => source.Source).ToArray();
            _workspace = await Task.Run(() => ComparisonWorkspace.Compare(sources));
            PresentFolderWorkspace(_workspace);
            SelectInitialFiles();
            await RebuildFileComparisonAsync(resetTarget);

            var movedFiles = _workspace.Relationships.Count(link => link.Kind is WorkspaceRelationshipKind.FileMoved or WorkspaceRelationshipKind.FileMovedEdited);
            var movedFolders = _workspace.Relationships.Count(link => link.Kind == WorkspaceRelationshipKind.FolderMoved);
            StatusLabel.Text = $"{sources.Length} sources · {_workspace.Rows.Count} aligned files · {movedFiles} file moves · {movedFolders} folder moves";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PresentFolderWorkspace(WorkspaceComparison workspace)
    {
        _suppressFolderSelection = true;
        Panes.Clear();
        for (var sourceIndex = 0; sourceIndex < workspace.Sources.Count; sourceIndex++)
        {
            var loaded = _loadedSources[sourceIndex];
            var pane = new WorkspacePaneViewModel(sourceIndex, workspace.Sources[sourceIndex].Id, workspace.Sources[sourceIndex].Label, loaded.RootPath);
            for (var rowIndex = 0; rowIndex < workspace.Rows.Count; rowIndex++)
            {
                var cell = workspace.Rows[rowIndex].Cells[sourceIndex];
                pane.FolderRows.Add(FolderRowViewModel.From(rowIndex, cell, workspace.Relationships));
            }
            pane.FolderChangeCount = pane.FolderRows.Count(row => row.IsChanged);
            Panes.Add(pane);
        }
        _suppressFolderSelection = false;

        var rowCount = Math.Max(1, workspace.Rows.Count);
        FolderLinksControl.PaneCount = workspace.Sources.Count;
        FolderLinksControl.Links = workspace.Relationships.Select(link => new VisualRelationship(
            link.SourceIndex,
            link.TargetIndex,
            (link.SourceRow + 0.5) / rowCount,
            (link.TargetRow + 0.5) / rowCount,
            link.Kind switch
            {
                WorkspaceRelationshipKind.FolderMoved => "folder",
                WorkspaceRelationshipKind.FileMovedEdited => "edited",
                _ => "file",
            })).ToArray();
        FolderLinksControl.InvalidateVisual();

        FolderSummaryLabel.Text = $"{workspace.Rows.Count} aligned entries across {workspace.Sources.Count} panes";
    }

    private void SelectInitialFiles()
    {
        if (_workspace is null || _workspace.Rows.Count == 0) return;
        _suppressFolderSelection = true;
        var preferredRow = _workspace.Rows
            .Select((row, index) => (row, index))
            .OrderByDescending(item => item.row.Cells.Count(cell => cell is not null))
            .First().index;

        for (var sourceIndex = 0; sourceIndex < Panes.Count; sourceIndex++)
        {
            var selected = Panes[sourceIndex].FolderRows.FirstOrDefault(row => row.LogicalRowIndex == preferredRow && !row.IsEmpty)
                ?? Panes[sourceIndex].FolderRows.FirstOrDefault(row => !row.IsEmpty);
            Panes[sourceIndex].SelectedFolderRow = selected;
            LoadPaneFile(Panes[sourceIndex]);
        }
        _suppressFolderSelection = false;
    }

    private async void OnFolderSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressFolderSelection || sender is not ListBox { DataContext: WorkspacePaneViewModel pane }) return;
        if (pane.SelectedFolderRow is null or { IsEmpty: true }) return;

        LoadPaneFile(pane);
        await RebuildFileComparisonAsync(resetTarget: pane.Index == 0);
    }

    private void LoadPaneFile(WorkspacePaneViewModel pane)
    {
        var row = pane.SelectedFolderRow;
        if (row is null || row.IsEmpty) return;
        var content = _loadedSources[pane.Index].Source.Files[row.RelativePath];
        pane.CurrentContent = content;
        pane.SelectedPath = row.RelativePath;
        pane.RawLines = TextFileLoader.Load(content).Lines;
    }

    private async Task RebuildFileComparisonAsync(bool resetTarget)
    {
        var available = Panes.Where(pane => pane.CurrentContent is not null).ToArray();
        if (available.Length == 0) return;

        SetBusy(true, "Comparing selected files and building semantic blocks…");
        try
        {
            foreach (var pane in Panes)
            {
                pane.Lines.Clear();
                pane.Blocks.Clear();
            }

            var states = Panes.ToDictionary(pane => pane.Index, pane => Enumerable.Repeat(LineVisualKind.Unchanged, pane.RawLines.Length).ToArray());
            var reference = available[0];
            var links = new List<VisualRelationship>();
            var whitespace = WhitespaceSelectorControl.SelectedItem is WhitespaceMode selectedWhitespace ? selectedWhitespace : WhitespaceMode.None;
            var detection = DetectionSelectorControl.SelectedItem is DetectionMode selectedDetection ? selectedDetection : DetectionMode.Balanced;
            var ignoreCase = IgnoreCaseCheckControl.IsChecked == true;

            foreach (var target in available.Skip(1))
            {
                var result = await Task.Run(() => FileComparer.CompareSourceFiles(
                    reference.CurrentContent!,
                    target.CurrentContent!,
                    reference.SelectedPath,
                    target.SelectedPath,
                    ignoreCase,
                    whitespace,
                    detection));
                ApplyLineStates(states[reference.Index], states[target.Index], result.Comparison);

                foreach (var block in result.Comparison.MovedBlocks)
                {
                    links.Add(new VisualRelationship(
                        reference.Index,
                        target.Index,
                        Position(block.OldStart, reference.RawLines.Length),
                        Position(block.NewStart, target.RawLines.Length),
                        "block"));
                }
            }

            foreach (var pane in Panes)
            {
                for (var index = 0; index < pane.RawLines.Length; index++)
                {
                    pane.Lines.Add(SourceLineViewModel.From(index, pane.RawLines[index], states[pane.Index][index]));
                }
                pane.Blocks.AddRange(BuildBlocks(pane, states[pane.Index]));
                pane.SelectedLine = pane.Lines.FirstOrDefault(line => line.IsChanged) ?? pane.Lines.FirstOrDefault();
            }

            BlockLinksControl.PaneCount = Panes.Count;
            BlockLinksControl.Links = links;
            BlockLinksControl.InvalidateVisual();

            if (resetTarget || _mergeDocument is null)
            {
                _mergeDocument = new InteractiveMergeDocument(reference.RawLines);
                TargetPathLabel.Text = $"result/{Path.GetFileName(reference.SelectedPath)}";
                RefreshMergeTarget();
            }

            FileSummaryLabel.Text = string.Join(" ↔ ", available.Select(pane => pane.SelectedPath));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static void ApplyLineStates(LineVisualKind[] oldStates, LineVisualKind[] newStates, FileComparisonResult comparison)
    {
        foreach (var change in comparison.Changes)
        {
            if (change.OldIndex is { } oldIndex)
            {
                Upgrade(oldStates, oldIndex, change.ChangeType switch
                {
                    ChangeType.Removed => LineVisualKind.Removed,
                    ChangeType.Edited => LineVisualKind.Edited,
                    _ => LineVisualKind.Unchanged,
                });
            }
            if (change.NewIndex is { } newIndex)
            {
                Upgrade(newStates, newIndex, change.ChangeType switch
                {
                    ChangeType.Added => LineVisualKind.Added,
                    ChangeType.Edited => LineVisualKind.Edited,
                    _ => LineVisualKind.Unchanged,
                });
            }
        }

        foreach (var block in comparison.MovedBlocks)
        {
            for (var index = block.OldStart; index <= block.OldEnd && index < oldStates.Length; index++) Upgrade(oldStates, index, LineVisualKind.Moved);
            for (var index = block.NewStart; index <= block.NewEnd && index < newStates.Length; index++) Upgrade(newStates, index, LineVisualKind.Moved);
        }
    }

    private static void Upgrade(LineVisualKind[] states, int index, LineVisualKind candidate)
    {
        if (candidate > states[index]) states[index] = candidate;
    }

    private static IEnumerable<MergeSourceBlock> BuildBlocks(WorkspacePaneViewModel pane, LineVisualKind[] states)
    {
        var index = 0;
        while (index < states.Length)
        {
            if (states[index] == LineVisualKind.Unchanged)
            {
                index++;
                continue;
            }

            var start = index;
            var kind = states[index];
            while (index + 1 < states.Length && states[index + 1] == kind) index++;
            var end = index;
            yield return new MergeSourceBlock(
                pane.Id,
                pane.SelectedPath,
                start,
                end,
                pane.RawLines[start..(end + 1)],
                kind switch
                {
                    LineVisualKind.Added => ChangeType.Added,
                    LineVisualKind.Removed => ChangeType.Removed,
                    LineVisualKind.Moved => ChangeType.Moved,
                    _ => ChangeType.Edited,
                });
            index++;
        }
    }

    private static double Position(int line, int lineCount) => (line + 0.5) / Math.Max(1, lineCount);

    private void OnInsertBlock(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WorkspacePaneViewModel pane } || _mergeDocument is null) return;
        var block = pane.GetSelectedBlock();
        if (block is null) return;

        var targetIndex = MergeTargetListControl.SelectedIndex >= 0
            ? MergeTargetListControl.SelectedIndex + 1
            : _mergeDocument.Lines.Count;
        _mergeDocument.Insert(block, targetIndex);
        RefreshMergeTarget($"Inserted {block.LineCount} lines from {block.SourcePath}:{block.StartLine + 1}");
    }

    private void OnReplaceBlock(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WorkspacePaneViewModel pane } || _mergeDocument is null) return;
        var block = pane.GetSelectedBlock();
        if (block is null) return;

        var targetStart = Math.Max(0, MergeTargetListControl.SelectedIndex);
        var replaceCount = Math.Min(block.LineCount, _mergeDocument.Lines.Count - targetStart);
        _mergeDocument.Replace(block, targetStart, replaceCount);
        RefreshMergeTarget($"Replaced {replaceCount} lines with {block.LineCount} lines from {block.SourcePath}:{block.StartLine + 1}");
    }

    private void OnUndoMerge(object? sender, RoutedEventArgs e)
    {
        if (_mergeDocument?.Undo() == true) RefreshMergeTarget("Undid last merge edit");
    }

    private async void OnExportTarget(object? sender, RoutedEventArgs e)
    {
        if (_mergeDocument is null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export reconstructed merge target",
            SuggestedFileName = Path.GetFileName(Panes.FirstOrDefault()?.SelectedPath ?? "merged.txt"),
        });
        if (file is null) return;

        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(string.Join(Environment.NewLine, _mergeDocument.Lines));
        StatusLabel.Text = $"Exported merge target to {file.Name}";
    }

    private void RefreshMergeTarget(string? status = null)
    {
        MergeLines.Clear();
        if (_mergeDocument is not null)
        {
            for (var index = 0; index < _mergeDocument.Lines.Count; index++)
            {
                MergeLines.Add(new MergeLineViewModel(index + 1, _mergeDocument.Lines[index]));
            }
            MergeStatusLabel.Text = status ?? (_mergeDocument.CanUndo ? $"{_mergeDocument.History.Count} merge edits" : "No merge edits");
        }
    }

    private void OnPreviousChange(object? sender, RoutedEventArgs e) => NavigateChange(-1);
    private void OnNextChange(object? sender, RoutedEventArgs e) => NavigateChange(1);

    private void NavigateChange(int direction)
    {
        var pane = Panes.FirstOrDefault(candidate => candidate.Lines.Count > 0);
        if (pane is null) return;
        for (var attempts = 0; attempts < pane.Lines.Count; attempts++)
        {
            _currentChange = (_currentChange + direction + pane.Lines.Count) % pane.Lines.Count;
            if (!pane.Lines[_currentChange].IsChanged) continue;
            pane.SelectedLine = pane.Lines[_currentChange];
            return;
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        BusyIndicatorControl.IsVisible = busy;
        if (status is not null) StatusLabel.Text = status;
    }

    private sealed record LoadedSource(WorkspaceSource Source, string RootPath, bool IsFolder);
}

public sealed class WorkspacePaneViewModel : NotifyObject
{
    private FolderRowViewModel? _selectedFolderRow;
    private SourceLineViewModel? _selectedLine;
    private string _selectedPath = "No file selected";
    private int _folderChangeCount;

    public WorkspacePaneViewModel(int index, string id, string title, string rootPath)
    {
        Index = index;
        Id = id;
        Title = title;
        RootPath = rootPath;
        Ordinal = (index + 1).ToString();
        AccentForeground = Palette.PaneForeground(index);
        AccentBackground = Palette.PaneBackground(index);
    }

    public int Index { get; }
    public string Id { get; }
    public string Title { get; }
    public string RootPath { get; }
    public string Ordinal { get; }
    public IBrush AccentForeground { get; }
    public IBrush AccentBackground { get; }
    public ObservableCollection<FolderRowViewModel> FolderRows { get; } = [];
    public ObservableCollection<SourceLineViewModel> Lines { get; } = [];
    public List<MergeSourceBlock> Blocks { get; } = [];
    public byte[]? CurrentContent { get; set; }
    public string[] RawLines { get; set; } = [];

    public int FolderChangeCount
    {
        get => _folderChangeCount;
        set => Set(ref _folderChangeCount, value);
    }

    public FolderRowViewModel? SelectedFolderRow
    {
        get => _selectedFolderRow;
        set => Set(ref _selectedFolderRow, value);
    }

    public SourceLineViewModel? SelectedLine
    {
        get => _selectedLine;
        set
        {
            if (Set(ref _selectedLine, value)) Raise(nameof(SelectedBlockLabel));
        }
    }

    public string SelectedPath
    {
        get => _selectedPath;
        set => Set(ref _selectedPath, value);
    }

    public string SelectedBlockLabel
    {
        get
        {
            var block = GetSelectedBlock();
            return block is null ? "Select a line" : $"{block.ChangeType} · {block.LineCount} lines";
        }
    }

    public MergeSourceBlock? GetSelectedBlock()
    {
        if (SelectedLine is null) return null;
        return Blocks.FirstOrDefault(block => SelectedLine.Index >= block.StartLine && SelectedLine.Index <= block.EndLine)
            ?? new MergeSourceBlock(Id, SelectedPath, SelectedLine.Index, SelectedLine.Index, [SelectedLine.Text], ChangeType.Unchanged);
    }
}

public sealed record FolderRowViewModel(
    int LogicalRowIndex,
    string RelativePath,
    string DisplayPath,
    string Icon,
    string Status,
    IBrush Background,
    IBrush StatusForeground,
    bool IsChanged,
    bool IsEmpty)
{
    public static FolderRowViewModel From(int rowIndex, WorkspaceCell? cell, IReadOnlyList<WorkspaceRelationship> relationships)
    {
        if (cell is null)
        {
            return new FolderRowViewModel(rowIndex, string.Empty, string.Empty, string.Empty, string.Empty, Brushes.Transparent, Brushes.Transparent, false, true);
        }

        var sourceMove = relationships.FirstOrDefault(link => link.SourceIndex == cell.SourceIndex && link.SourcePath == cell.RelativePath);
        var status = sourceMove is not null ? "moved →" : cell.ChangeType switch
        {
            FolderChangeType.Added => "added",
            FolderChangeType.Removed => "removed",
            FolderChangeType.Changed => "changed",
            FolderChangeType.Moved => "← moved",
            FolderChangeType.MovedEdited => "← moved + edited",
            FolderChangeType.Copied => "copied",
            _ => string.Empty,
        };
        var kind = sourceMove?.Kind == WorkspaceRelationshipKind.FolderMoved
            ? FolderChangeType.Moved
            : cell.ChangeType;

        return new FolderRowViewModel(
            rowIndex,
            cell.RelativePath,
            cell.RelativePath,
            "▧",
            status,
            Palette.FolderBackground(kind, sourceMove is not null),
            Palette.FolderForeground(kind, sourceMove is not null),
            status.Length > 0,
            false);
    }
}

public enum LineVisualKind { Unchanged, Added, Removed, Edited, Moved }

public sealed record SourceLineViewModel(
    int Index,
    int Number,
    string Text,
    string Marker,
    IBrush Background,
    IBrush MarkerForeground,
    bool IsChanged)
{
    public static SourceLineViewModel From(int index, string text, LineVisualKind kind) => new(
        index,
        index + 1,
        text,
        kind switch
        {
            LineVisualKind.Added => "+",
            LineVisualKind.Removed => "−",
            LineVisualKind.Edited => "~",
            LineVisualKind.Moved => "↝",
            _ => string.Empty,
        },
        Palette.LineBackground(kind),
        Palette.LineForeground(kind),
        kind != LineVisualKind.Unchanged);
}

public sealed record MergeLineViewModel(int Number, string Text)
{
    public IBrush Background { get; } = Brushes.Transparent;
}

public abstract class NotifyObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(propertyName);
        return true;
    }

    protected void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal static class Palette
{
    private static readonly IBrush Transparent = Brushes.Transparent;
    private static readonly IBrush Green = Brush("#3568B984");
    private static readonly IBrush Red = Brush("#35D97882");
    private static readonly IBrush Amber = Brush("#35E0B55B");
    private static readonly IBrush Purple = Brush("#359B7BEA");
    private static readonly IBrush Blue = Brush("#356D9EEB");
    private static readonly IBrush GreenText = Brush("#58B77D");
    private static readonly IBrush RedText = Brush("#D46F79");
    private static readonly IBrush AmberText = Brush("#C99A36");
    private static readonly IBrush PurpleText = Brush("#9173D6");
    private static readonly IBrush BlueText = Brush("#5D8DD3");

    public static IBrush PaneForeground(int index) => index switch
    {
        0 => BlueText,
        1 => GreenText,
        2 => PurpleText,
        _ => AmberText,
    };

    public static IBrush PaneBackground(int index) => index switch
    {
        0 => Blue,
        1 => Green,
        2 => Purple,
        _ => Amber,
    };

    public static IBrush FolderBackground(FolderChangeType kind, bool sourceMove) => sourceMove ? Purple : kind switch
    {
        FolderChangeType.Added => Green,
        FolderChangeType.Removed => Red,
        FolderChangeType.Changed => Amber,
        FolderChangeType.Moved => Purple,
        FolderChangeType.MovedEdited => Amber,
        FolderChangeType.Copied => Blue,
        _ => Transparent,
    };

    public static IBrush FolderForeground(FolderChangeType kind, bool sourceMove) => sourceMove ? PurpleText : kind switch
    {
        FolderChangeType.Added => GreenText,
        FolderChangeType.Removed => RedText,
        FolderChangeType.Changed => AmberText,
        FolderChangeType.Moved => PurpleText,
        FolderChangeType.MovedEdited => AmberText,
        FolderChangeType.Copied => BlueText,
        _ => Brushes.Transparent,
    };

    public static IBrush LineBackground(LineVisualKind kind) => kind switch
    {
        LineVisualKind.Added => Green,
        LineVisualKind.Removed => Red,
        LineVisualKind.Edited => Amber,
        LineVisualKind.Moved => Purple,
        _ => Transparent,
    };

    public static IBrush LineForeground(LineVisualKind kind) => kind switch
    {
        LineVisualKind.Added => GreenText,
        LineVisualKind.Removed => RedText,
        LineVisualKind.Edited => AmberText,
        LineVisualKind.Moved => PurpleText,
        _ => Brushes.Transparent,
    };

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}

