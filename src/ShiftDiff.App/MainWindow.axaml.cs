using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ShiftDiff.App.Controls;
using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.App;

// The window is a thin shell over ShellViewModel: it translates gestures into
// view-model calls and view-model state into controls. All comparison and
// presentation logic lives in ShiftDiff.Ui, where it is unit-tested.
public sealed partial class MainWindow : Window
{
    public static readonly StyledProperty<bool> UseEmojiMarkersProperty =
        AvaloniaProperty.Register<MainWindow, bool>(nameof(UseEmojiMarkers), true);

    private readonly ShellViewModel _shell = new();
    private readonly ObservableCollection<DiffRow> _rows = [];
    private readonly ObservableCollection<FileListEntry> _files = [];
    private readonly ObservableCollection<MovedBlockInfo> _movedBlocks = [];
    private readonly ObservableCollection<DetailEntry> _detailEntries = [];
    private ScrollViewer? _diffScroll;
    private bool _suppressSelection;

    public MainWindow() : this([]) { }

    public MainWindow(IReadOnlyList<string> args)
    {
        InitializeComponent();

        NextChangeCommand = new RelayCommand(_shell.GoToNextChange);
        PreviousChangeCommand = new RelayCommand(_shell.GoToPreviousChange);
        NextConflictCommand = new RelayCommand(_shell.GoToNextConflict);
        NextMovedCommand = new RelayCommand(_shell.GoToNextMovedBlock);
        JumpToPairCommand = new RelayCommand(_shell.GoToPairedBlock);
        RefreshCommand = new RelayCommand(() => _ = _shell.RefreshAsync());
        FocusSearchCommand = new RelayCommand(() => SearchBox.Focus());
        SwapCommand = new RelayCommand(() => _ = SwapAsync());
        ZoomInCommand = new RelayCommand(() => Zoom(1));
        ZoomOutCommand = new RelayCommand(() => Zoom(-1));

        DiffList.ItemsSource = _rows;
        FileList.ItemsSource = _files;
        MovedBlocksList.ItemsSource = _movedBlocks;
        DetailEntries.ItemsSource = _detailEntries;

        PopulateSelectors();
        _shell.PropertyChanged += OnShellPropertyChanged;
        _shell.FileCollection.CollectionChanged += (_, _) => RefreshFileList();
        Overview.PositionPicked += (_, position) => _shell.GoToOverviewPosition(position);

        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);

        DiffList.DoubleTapped += OnRowDoubleTapped;

        Opened += async (_, _) =>
        {
            HookScrollViewer();
            if (args.Count > 0) await _shell.OpenDroppedAsync([.. args]);
        };
    }

    public ICommand NextChangeCommand { get; }

    public ICommand PreviousChangeCommand { get; }

    public ICommand NextConflictCommand { get; }

    public ICommand NextMovedCommand { get; }

    public ICommand JumpToPairCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand FocusSearchCommand { get; }

    public ICommand SwapCommand { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    /// <summary>Bound by the row template to pick between emoji and text markers (FR-043).</summary>
    public bool UseEmojiMarkers
    {
        get => GetValue(UseEmojiMarkersProperty);
        set => SetValue(UseEmojiMarkersProperty, value);
    }

    // --- setup -------------------------------------------------------------

    private void PopulateSelectors()
    {
        DetectionSelector.ItemsSource = Enum.GetValues<DetectionMode>();
        DetectionSelector.SelectedItem = _shell.Settings.Detection;
        WhitespaceSelector.ItemsSource = Enum.GetValues<WhitespaceMode>();
        WhitespaceSelector.SelectedItem = _shell.Settings.Whitespace;
        LayoutSelector.ItemsSource = new[] { "Side by side", "Unified" };
        LayoutSelector.SelectedIndex = 0;
        ThemeSelector.ItemsSource = Enum.GetValues<ThemeMode>();
        ThemeSelector.SelectedItem = _shell.Settings.Theme;
        FilterSelector.ItemsSource = new[] { "All rows", "Only changes", "Added", "Removed", "Edited", "Moved", "Conflicts" };
        FilterSelector.SelectedIndex = 0;

        CollapseCheck.IsChecked = _shell.Settings.CollapseUnchanged;
        SyntaxCheck.IsChecked = _shell.Settings.SyntaxHighlighting;
        EmojiCheck.IsChecked = _shell.Settings.ShowEmojiMarkers;
        IgnoreCaseCheck.IsChecked = _shell.Settings.IgnoreCase;
        SidebarCheck.IsChecked = true;
        InspectorCheck.IsChecked = true;
        WrapCheck.IsChecked = _shell.Settings.WordWrap;
        ContrastCheck.IsChecked = _shell.Settings.HighContrast;
        UseEmojiMarkers = _shell.Settings.ShowEmojiMarkers;
        ApplyTextSettings();
    }

    // The settings that only change how the panes are drawn are applied here
    // rather than by rebuilding the document.
    private void ApplyTextSettings()
    {
        DiffList.FontSize = _shell.Settings.FontSize;

        // A WrapPanel only wraps when the row is width-constrained, so switching
        // the horizontal scrollbar off is what turns wrapping on.
        ScrollViewer.SetHorizontalScrollBarVisibility(
            DiffList,
            _shell.Settings.WordWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);

        Classes.Set("highcontrast", _shell.Settings.HighContrast);
        ZoomText.Text = $"{_shell.Settings.FontSize:N0} px";
    }

    private void Zoom(int steps)
    {
        _shell.Settings.FontSize += steps;
        ApplyTextSettings();
    }

    private void HookScrollViewer()
    {
        _diffScroll = DiffList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (_diffScroll is null) return;

        _diffScroll.ScrollChanged += (_, _) =>
        {
            UpdateViewportIndicator();
            UpdateRelationshipLinks();
        };
        UpdateViewportIndicator();
        UpdateRelationshipLinks();
    }

    private void UpdateViewportIndicator()
    {
        if (_diffScroll is null || _rows.Count == 0) return;

        var extent = _diffScroll.Extent.Height;
        if (extent <= 0) return;

        Overview.ViewportStart = _diffScroll.Offset.Y / extent;
        Overview.ViewportEnd = (_diffScroll.Offset.Y + _diffScroll.Viewport.Height) / extent;
    }

    // Araxis-style pane linking: the moved-block threads are drawn against the
    // visible viewport, so they follow the panes as they scroll.
    private void UpdateRelationshipLinks()
    {
        if (_rows.Count == 0 || _shell.Links.Count == 0)
        {
            Relationships.Links = [];
            return;
        }

        Relationships.PaneCount = Math.Max(2, _shell.PaneTitles.Count);

        var rowHeight = _diffScroll is { Extent.Height: > 0 } ? _diffScroll.Extent.Height / _rows.Count : 0;
        var viewport = _diffScroll?.Viewport.Height ?? Bounds.Height;
        var offset = _diffScroll?.Offset.Y ?? 0;
        if (rowHeight <= 0 || viewport <= 0)
        {
            Relationships.Links = [];
            return;
        }

        var links = new List<VisualRelationship>();
        foreach (var link in _shell.Links)
        {
            var source = PositionOf(link.SourceRow);
            var target = PositionOf(link.TargetRow);
            if (source is null || target is null) continue;

            links.Add(new VisualRelationship(
                link.SourcePane, link.TargetPane, source.Value, target.Value,
                link.Kind == ChangeType.MovedEdited ? "edited" : "block"));
        }

        Relationships.Links = links;

        double? PositionOf(int documentRow)
        {
            var visibleRow = VisibleIndexOfDocumentRow(documentRow);
            if (visibleRow < 0) return null;

            var position = (visibleRow * rowHeight + rowHeight / 2 - offset) / viewport;
            return position is >= -0.05 and <= 1.05 ? position : null;
        }
    }

    private int VisibleIndexOfDocumentRow(int documentRow)
    {
        if (documentRow < 0 || documentRow >= _shell.Document.Rows.Count) return -1;

        var row = _shell.Document.Rows[documentRow];
        for (var i = 0; i < _rows.Count; i++)
        {
            if (ReferenceEquals(_rows[i], row)) return i;
        }

        return -1;
    }

    // --- view-model plumbing ----------------------------------------------

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.VisibleRows):
                ReplaceRows();
                break;
            case nameof(ShellViewModel.Document):
                ReplaceMovedBlocks();
                UpdatePaneHeaders();
                UpdateMergeState();
                Overview.Stripes = _shell.Overview;
                LanguageText.Text = $"Language: {_shell.LanguageName}";
                SummaryText.Text = _shell.Summary.Text;
                break;
            case nameof(ShellViewModel.StatusText):
                StatusText.Text = _shell.StatusText;
                break;
            case nameof(ShellViewModel.SessionTitle):
                SessionTitleText.Text = _shell.SessionTitle;
                Title = $"ShiftDiff — {_shell.SessionTitle}";
                break;
            case nameof(ShellViewModel.IsBusy):
                BusyIndicator.IsVisible = _shell.IsBusy;
                CancelButton.IsVisible = _shell.IsBusy;
                break;
            case nameof(ShellViewModel.Details):
                UpdateDetails();
                break;
            case nameof(ShellViewModel.SelectedRow):
                SyncSelection();
                break;
            case nameof(ShellViewModel.ChangePositionText):
                ChangePositionText.Text = _shell.ChangePositionText;
                break;
            case nameof(ShellViewModel.MergedLineCount):
            case nameof(ShellViewModel.CanResolve):
                UpdateMergeState();
                break;
            case nameof(ShellViewModel.IsRepositorySession):
            case nameof(ShellViewModel.FromRevision):
            case nameof(ShellViewModel.ToRevision):
                UpdateRepositoryBar();
                break;
        }
    }

    private void ReplaceRows()
    {
        _suppressSelection = true;
        _rows.Clear();
        foreach (var row in _shell.VisibleRows) _rows.Add(row);
        _suppressSelection = false;
        EmptyState.IsVisible = _rows.Count == 0;
        UpdateViewportIndicator();
        UpdateRelationshipLinks();
    }

    private void ReplaceMovedBlocks()
    {
        _movedBlocks.Clear();
        foreach (var block in _shell.MovedBlocks) _movedBlocks.Add(block);
    }

    private void UpdatePaneHeaders()
    {
        PaneHeaders.ItemsSource = _shell.PaneTitles;

        // The unified projection only applies to a two-pane comparison.
        LayoutSelector.IsEnabled = _shell.PaneTitles.Count <= 2;
    }

    private void UpdateDetails()
    {
        DetailTitleText.Text = _shell.Details.Title;
        DetailSubtitleText.Text = _shell.Details.Subtitle;
        _detailEntries.Clear();
        foreach (var entry in _shell.Details.Entries) _detailEntries.Add(entry);
    }

    private void SyncSelection()
    {
        if (_shell.SelectedRow < 0 || _shell.SelectedRow >= _rows.Count) return;

        _suppressSelection = true;
        DiffList.SelectedIndex = _shell.SelectedRow;
        DiffList.ScrollIntoView(_rows[_shell.SelectedRow]);
        _suppressSelection = false;

        Overview.CursorPosition = _rows.Count == 0 ? -1 : (double)_shell.SelectedRow / _rows.Count;
        ChangePositionText.Text = _shell.ChangePositionText;
    }

    // --- toolbar ----------------------------------------------------------

    private async void OnOpenPair(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick two files to compare",
            AllowMultiple = true,
        });

        var paths = files.Select(file => file.Path.LocalPath).ToArray();
        if (paths.Length >= 2) await _shell.OpenDroppedAsync(paths);
    }

    private async void OnOpenFolders(object? sender, RoutedEventArgs e)
    {
        var left = await PickFolderAsync("Pick the original folder");
        if (left is null) return;
        var right = await PickFolderAsync("Pick the changed folder");
        if (right is null) return;

        await _shell.OpenFolderPairAsync(left, right);
    }

    private async void OnOpenRepository(object? sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync("Pick a Git or SVN working copy") is { } path)
        {
            await _shell.OpenRepositoryAsync(path);
        }
    }

    private async Task SwapAsync()
    {
        if (_shell.OldTitle is not { } oldPath || _shell.NewTitle is not { } newPath) return;
        if (!File.Exists(oldPath) || !File.Exists(newPath))
        {
            // Repository and workspace sides are revisions, not files on disk.
            StatusText.Text = "Only a file pair can be swapped.";
            return;
        }

        await _shell.OpenFilePairAsync(newPath, oldPath);
    }

    // --- repository revisions (FR-030/FR-031) ------------------------------

    private async void OnCompareRevisions(object? sender, RoutedEventArgs e) => await CompareRevisionsAsync();

    private async void OnRevisionKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await CompareRevisionsAsync();
    }

    private Task CompareRevisionsAsync() =>
        _shell.OpenRevisionRangeAsync(FromRevisionBox.Text ?? string.Empty, ToRevisionBox.Text ?? string.Empty);

    private void UpdateRepositoryBar()
    {
        RevisionBar.IsVisible = _shell.IsRepositorySession;
        DropHintText.IsVisible = !_shell.IsRepositorySession;
        FromRevisionBox.Text = _shell.FromRevision;
        ToRevisionBox.Text = _shell.ToRevision;
    }

    private async void OnExportPatch(object? sender, RoutedEventArgs e)
    {
        if (_shell.OldTitle is not { } oldPath || _shell.NewTitle is not { } newPath) return;

        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export unified diff",
            SuggestedFileName = Path.GetFileNameWithoutExtension(newPath) + ".diff",
            DefaultExtension = "diff",
        });

        if (target is null) return;

        try
        {
            var result = FileComparer.CompareSourceFiles(
                await File.ReadAllBytesAsync(oldPath), await File.ReadAllBytesAsync(newPath), oldPath, newPath,
                _shell.Settings.IgnoreCase, _shell.Settings.Whitespace, _shell.Settings.Detection);
            var file = UnifiedDiffBuilder.Build(result.Comparison.Changes, oldPath, newPath, _shell.Settings.ContextLines);
            await File.WriteAllLinesAsync(target.Path.LocalPath, UnifiedDiffFormatter.Format(file));
            StatusText.Text = $"Patch written to {target.Path.LocalPath}";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Export failed: {exception.Message}";
        }
    }

    private void OnFirstChange(object? sender, RoutedEventArgs e)
    {
        _shell.SelectedRow = -1;
        _shell.GoToNextChange();
    }

    private void OnLastChange(object? sender, RoutedEventArgs e)
    {
        _shell.SelectedRow = _rows.Count;
        _shell.GoToPreviousChange();
    }

    private void OnPreviousChange(object? sender, RoutedEventArgs e) => _shell.GoToPreviousChange();

    private void OnNextChange(object? sender, RoutedEventArgs e) => _shell.GoToNextChange();

    private void OnNextConflict(object? sender, RoutedEventArgs e) => _shell.GoToNextConflict();

    private void OnNextMoved(object? sender, RoutedEventArgs e) => _shell.GoToNextMovedBlock();

    private void OnJumpToPair(object? sender, RoutedEventArgs e) => _shell.GoToPairedBlock();

    private void OnCancel(object? sender, RoutedEventArgs e) => _shell.CancelAnalysis();

    // --- interactive merge (FR-047) ---------------------------------------

    private void OnTakeLeftBlock(object? sender, RoutedEventArgs e) => TakeBlock(0);

    private void OnTakeRemoteBlock(object? sender, RoutedEventArgs e) => TakeBlock(2);

    private void TakeBlock(int pane)
    {
        if (!_shell.TakeSelectedBlock(pane)) StatusText.Text = "Select a changed line first.";
        UpdateMergeState();
    }

    private void OnUndoMerge(object? sender, RoutedEventArgs e)
    {
        _shell.UndoMerge();
        UpdateMergeState();
    }

    private async void OnSaveMerged(object? sender, RoutedEventArgs e)
    {
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the reconstructed result",
            SuggestedFileName = Path.GetFileName(_shell.NewTitle ?? "resolved.txt"),
        });

        if (target is null) return;

        // The picker already asked the user about replacing an existing file.
        _shell.SaveMergedResult(target.Path.LocalPath, overwrite: true);
        UpdateMergeState();
    }

    private void UpdateMergeState()
    {
        var merging = _shell.PaneTitles.Count == 3;

        ResolvePanel.IsVisible = _shell.CanResolve;
        TakeRemoteButton.IsVisible = merging;
        TakeLeftButton.Content = merging ? "◀ Take base" : "◀ Take left";
        MergeStateText.Text = _shell.CanUndoMerge
            ? $"Result edited · {_shell.MergedLineCount} lines"
            : merging ? "Result matches the local file" : "Result matches the right file";
    }

    // --- options ----------------------------------------------------------

    private void OnDetectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DetectionSelector.SelectedItem is DetectionMode mode)
        {
            _shell.Settings.Detection = mode;
            ModeText.Text = mode.ToString();
        }
    }

    private void OnWhitespaceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (WhitespaceSelector.SelectedItem is WhitespaceMode mode) _shell.Settings.Whitespace = mode;
    }

    private void OnIgnoreCaseChanged(object? sender, RoutedEventArgs e) =>
        _shell.Settings.IgnoreCase = IgnoreCaseCheck.IsChecked == true;

    private void OnCollapseChanged(object? sender, RoutedEventArgs e) =>
        _shell.Settings.CollapseUnchanged = CollapseCheck.IsChecked == true;

    private void OnSyntaxChanged(object? sender, RoutedEventArgs e) =>
        _shell.Settings.SyntaxHighlighting = SyntaxCheck.IsChecked == true;

    private void OnEmojiChanged(object? sender, RoutedEventArgs e)
    {
        _shell.Settings.ShowEmojiMarkers = EmojiCheck.IsChecked == true;
        UseEmojiMarkers = EmojiCheck.IsChecked == true;
    }

    private void OnWrapChanged(object? sender, RoutedEventArgs e)
    {
        _shell.Settings.WordWrap = WrapCheck.IsChecked == true;
        ApplyTextSettings();
    }

    private void OnContrastChanged(object? sender, RoutedEventArgs e)
    {
        _shell.Settings.HighContrast = ContrastCheck.IsChecked == true;
        ApplyTextSettings();
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e) => Zoom(1);

    private void OnZoomOut(object? sender, RoutedEventArgs e) => Zoom(-1);

    private void OnSidebarChanged(object? sender, RoutedEventArgs e) =>
        SidebarPanel.IsVisible = SidebarCheck.IsChecked == true && _shell.ShowFileList;

    private void OnInspectorChanged(object? sender, RoutedEventArgs e) =>
        InspectorPanel.IsVisible = InspectorCheck.IsChecked == true;

    private void OnLayoutChanged(object? sender, SelectionChangedEventArgs e) =>
        _shell.Settings.Layout = LayoutSelector.SelectedIndex == 1 ? PaneLayout.Unified : PaneLayout.SideBySide;

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Application.Current is null || ThemeSelector.SelectedItem is not ThemeMode theme) return;

        _shell.Settings.Theme = theme;
        Application.Current.RequestedThemeVariant = theme switch
        {
            ThemeMode.Dark => ThemeVariant.Dark,
            ThemeMode.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e) =>
        _shell.Filter = FilterSelector.SelectedIndex switch
        {
            1 => ChangeTypeFilter.OnlyChanges,
            2 => ChangeTypeFilter.Added,
            3 => ChangeTypeFilter.Removed,
            4 => ChangeTypeFilter.Edited,
            5 => ChangeTypeFilter.Moved,
            6 => ChangeTypeFilter.Conflict,
            _ => ChangeTypeFilter.All,
        };

    private void OnSearchChanged(object? sender, KeyEventArgs e) => _shell.SearchText = SearchBox.Text ?? string.Empty;

    private void OnFileFilterChanged(object? sender, KeyEventArgs e) => RefreshFileList();

    private void RefreshFileList()
    {
        var filter = FileFilterBox.Text ?? string.Empty;

        _suppressSelection = true;
        _files.Clear();
        foreach (var entry in _shell.Files.Where(entry =>
                     filter.Length == 0 || entry.DisplayPath.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            _files.Add(entry);
        }

        FileList.SelectedItem = _shell.SelectedFile;
        _suppressSelection = false;

        // A single-file session has nothing to pick from; keep the space for the diff.
        SidebarPanel.IsVisible = SidebarCheck.IsChecked == true && _shell.ShowFileList;
    }

    // --- selection --------------------------------------------------------

    private void OnRowSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;

        _shell.SelectedRow = DiffList.SelectedIndex;
        Overview.CursorPosition = _rows.Count == 0 ? -1 : (double)DiffList.SelectedIndex / _rows.Count;
    }

    private async void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DiffList.SelectedItem is DiffRow { Kind: DiffRowKind.Collapsed } row) await _shell.ExpandRegionAsync(row);
    }

    private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        if (FileList.SelectedItem is FileListEntry entry) _shell.SelectedFile = entry;
    }

    private void OnMovedBlockSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (MovedBlocksList.SelectedItem is MovedBlockInfo block) _shell.GoToMovedBlock(block);
    }

    // --- drag and drop ----------------------------------------------------

    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.TryGetFiles()?
            .Select(item => item.Path.LocalPath)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Take(4)
            .ToArray();

        if (paths is { Length: > 0 }) await _shell.OpenDroppedAsync(paths);
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.FirstOrDefault()?.Path.LocalPath;
    }
}
