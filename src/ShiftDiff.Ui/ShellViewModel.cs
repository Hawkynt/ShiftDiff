using System.Collections.ObjectModel;
using ShiftDiff.Core;
using ShiftDiff.Vcs;

namespace ShiftDiff.Ui;

// The application's whole interaction model, free of any UI framework so it can
// be driven from tests: what is loaded, what is shown, where the cursor is.
public sealed class ShellViewModel : ObservableObject
{
    private readonly List<FileListEntry> _files = [];
    private CancellationTokenSource? _analysis;
    private IComparisonSource? _source;
    private DiffDocument _document = DiffDocument.Empty;
    private IReadOnlyList<DiffRow> _visibleRows = [];
    private ChangeDetails _details = ChangeDetails.Empty;
    private FileListEntry? _selectedFile;
    private ChangeTypeFilter _filter = ChangeTypeFilter.All;
    private string _searchText = string.Empty;
    private string _statusText = "Drop files, folders or a repository to begin.";
    private string _sessionTitle = "ShiftDiff";
    private string? _oldTitle;
    private string? _newTitle;
    private bool _isBusy;
    private int _selectedRow = -1;

    public ShellViewModel(ComparisonSettings? settings = null)
    {
        Settings = settings ?? new ComparisonSettings();
        Settings.ComparisonAffectingChanged += (_, _) => _ = RefreshAsync();
        Settings.PresentationChanged += (_, _) => _ = RefreshAsync();
        Files = new ReadOnlyObservableCollection<FileListEntry>(FileCollection);
    }

    public ComparisonSettings Settings { get; }

    public ObservableCollection<FileListEntry> FileCollection { get; } = [];

    public ReadOnlyObservableCollection<FileListEntry> Files { get; }

    public DiffDocument Document
    {
        get => _document;
        private set
        {
            if (!SetProperty(ref _document, value)) return;
            Navigator = new ChangeNavigator(value);
            Raise(nameof(Navigator));
            Raise(nameof(Summary));
            Raise(nameof(MovedBlocks));
            Raise(nameof(Overview));
            Raise(nameof(LanguageName));
            Raise(nameof(PaneTitles));
        }
    }

    public ChangeNavigator Navigator { get; private set; } = new(DiffDocument.Empty);

    public IReadOnlyList<DiffRow> VisibleRows
    {
        get => _visibleRows;
        private set => SetProperty(ref _visibleRows, value);
    }

    public DiffSummary Summary => Document.Summary;

    public IReadOnlyList<MovedBlockInfo> MovedBlocks => Document.MovedBlocks;

    public IReadOnlyList<OverviewStripe> Overview => Document.Overview;

    public IReadOnlyList<string> PaneTitles => Document.PaneTitles;

    public string LanguageName => Document.LanguageName;

    public ChangeDetails Details
    {
        get => _details;
        private set => SetProperty(ref _details, value);
    }

    public string SessionTitle
    {
        get => _sessionTitle;
        private set => SetProperty(ref _sessionTitle, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? OldTitle
    {
        get => _oldTitle;
        private set => SetProperty(ref _oldTitle, value);
    }

    public string? NewTitle
    {
        get => _newTitle;
        private set => SetProperty(ref _newTitle, value);
    }

    public int SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!SetProperty(ref _selectedRow, value)) return;
            Details = ChangeDetailsBuilder.Build(Document, RowIndexInDocument(value));
            Raise(nameof(ChangePositionText));
        }
    }

    public FileListEntry? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (!SetProperty(ref _selectedFile, value)) return;
            if (value is not null) _ = CompareSelectedFileAsync();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;
            ApplyFilter();
        }
    }

    public ChangeTypeFilter Filter
    {
        get => _filter;
        set
        {
            if (!SetProperty(ref _filter, value)) return;
            ApplyFilter();
        }
    }

    public string ChangePositionText
    {
        get
        {
            var total = Document.ChangeRowIndices.Count;
            if (total == 0) return "no changes";
            var ordinal = Navigator.OrdinalOf(RowIndexInDocument(SelectedRow));
            return ordinal > 0 ? $"change {ordinal} of {total}" : $"{total} changes";
        }
    }

    // --- opening ----------------------------------------------------------

    public Task OpenFilePairAsync(string oldPath, string newPath) =>
        OpenAsync(new FilePairSource(oldPath, newPath));

    public Task OpenFolderPairAsync(string basePath, string targetPath) =>
        OpenAsync(new FolderComparisonSource(basePath, targetPath));

    public Task OpenRepositoryAsync(
        string path, string fromRevision = VcsRevisions.Head, string toRevision = VcsRevisions.WorkingTree)
    {
        var workspace = VcsWorkspace.Open(path);
        if (workspace is null)
        {
            StatusText = $"No Git or SVN repository found at {path}";
            return Task.CompletedTask;
        }

        return OpenAsync(new VcsComparisonSource(workspace, fromRevision, toRevision));
    }

    /// <summary>FR-041: works out what was dropped and opens the matching mode (AC-008).</summary>
    public Task OpenDroppedAsync(IReadOnlyList<string> paths)
    {
        switch (paths.Count)
        {
            case 0:
                return Task.CompletedTask;
            case 1 when Directory.Exists(paths[0]):
                return OpenRepositoryOrFolderAsync(paths[0]);
            case 1:
                StatusText = "Drop a second file to compare against.";
                return Task.CompletedTask;
            case 2 when Directory.Exists(paths[0]) && Directory.Exists(paths[1]):
                return OpenFolderPairAsync(paths[0], paths[1]);
            case 2:
                return OpenFilePairAsync(paths[0], paths[1]);
            case 3:
                return OpenThreeWayAsync(paths[0], paths[1], paths[2]);
            default:
                return OpenFourWayAsync(paths[0], paths[1], paths[2], paths[3]);
        }
    }

    public async Task OpenThreeWayAsync(string basePath, string localPath, string remotePath)
    {
        await RunAnalysisAsync(token =>
        {
            var changes = ThreeWayComparer.Compare(
                File.ReadAllLines(basePath), File.ReadAllLines(localPath), File.ReadAllLines(remotePath),
                Settings.IgnoreCase, Settings.Whitespace);
            token.ThrowIfCancellationRequested();

            var language = SourceLanguageDetector.Detect(localPath);
            return DiffDocumentBuilder.BuildThreeWay(
                changes, Settings, language,
                Path.GetFileName(basePath), Path.GetFileName(localPath), Path.GetFileName(remotePath));
        });

        Settings.Layout = PaneLayout.ThreeWay;
        SessionTitle = $"{Path.GetFileName(localPath)} ↔ {Path.GetFileName(remotePath)}";
        _source = null;
        FileCollection.Clear();
    }

    public async Task OpenFourWayAsync(string basePath, string localPath, string remotePath, string targetPath)
    {
        await RunAnalysisAsync(token =>
        {
            var changes = ThreeWayComparer.Compare(
                File.ReadAllLines(basePath), File.ReadAllLines(localPath), File.ReadAllLines(remotePath),
                Settings.IgnoreCase, Settings.Whitespace);
            token.ThrowIfCancellationRequested();

            return DiffDocumentBuilder.BuildFourWay(
                changes, File.ReadAllLines(targetPath), Settings, SourceLanguageDetector.Detect(localPath),
                Path.GetFileName(basePath), Path.GetFileName(localPath),
                Path.GetFileName(remotePath), Path.GetFileName(targetPath));
        });

        Settings.Layout = PaneLayout.FourWay;
        SessionTitle = $"4-way: {Path.GetFileName(targetPath)}";
        _source = null;
        FileCollection.Clear();
    }

    public Task OpenRepositoryOrFolderAsync(string path) =>
        VcsWorkspace.Open(path) is { } workspace
            ? OpenAsync(new VcsComparisonSource(workspace))
            : Task.FromResult(StatusText = $"Drop a second folder to compare {Path.GetFileName(path)} against.");

    public async Task OpenAsync(IComparisonSource source)
    {
        _source = source;
        SessionTitle = source.Title;
        FileCollection.Clear();
        foreach (var entry in source.Entries) FileCollection.Add(entry);

        StatusText = FileCollection.Count switch
        {
            0 => "No differences found.",
            1 => source.Title,
            _ => $"{FileCollection.Count} changed file(s)",
        };

        SelectedFile = FileCollection.FirstOrDefault();
        if (SelectedFile is null)
        {
            Document = DiffDocument.Empty;
            ApplyFilter();
            return;
        }

        await CompareSelectedFileAsync();
    }

    public Task RefreshAsync() => _source is null || SelectedFile is null ? Task.CompletedTask : CompareSelectedFileAsync();

    /// <summary>FR-052: abandons a running analysis.</summary>
    public void CancelAnalysis() => _analysis?.Cancel();

    // --- navigation -------------------------------------------------------

    public void GoToNextChange() => SelectDocumentRow(Navigator.Next(RowIndexInDocument(SelectedRow)));

    public void GoToPreviousChange() => SelectDocumentRow(Navigator.Previous(RowIndexInDocument(SelectedRow)));

    public void GoToNextConflict() => SelectDocumentRow(Navigator.NextConflict(RowIndexInDocument(SelectedRow)));

    public void GoToPreviousConflict() => SelectDocumentRow(Navigator.PreviousConflict(RowIndexInDocument(SelectedRow)));

    public void GoToNextMovedBlock() => SelectDocumentRow(Navigator.NextMoved(RowIndexInDocument(SelectedRow)));

    public void GoToPreviousMovedBlock() => SelectDocumentRow(Navigator.PreviousMoved(RowIndexInDocument(SelectedRow)));

    public void GoToPairedBlock() => SelectDocumentRow(Navigator.PairedRow(RowIndexInDocument(SelectedRow)));

    public void GoToMovedBlock(MovedBlockInfo block) => SelectDocumentRow(block.NewRowIndex >= 0 ? block.NewRowIndex : block.OldRowIndex);

    /// <summary>Maps a normalized overview position (0..1) to a row and selects it.</summary>
    public void GoToOverviewPosition(double position)
    {
        if (Document.Rows.Count == 0) return;

        var index = (int)Math.Clamp(position * Document.Rows.Count, 0, Document.Rows.Count - 1);
        SelectDocumentRow(index);
    }

    /// <summary>Expands one folded region back into its individual lines.</summary>
    public void ExpandRegion(DiffRow collapsedRow)
    {
        if (collapsedRow.Kind != DiffRowKind.Collapsed) return;

        Settings.CollapseUnchanged = false;
    }

    // --- internals --------------------------------------------------------

    private async Task CompareSelectedFileAsync()
    {
        if (_source is null || SelectedFile is not { } entry) return;

        var input = _source.Load(entry);
        OldTitle = input.OldTitle;
        NewTitle = input.NewTitle;

        await RunAnalysisAsync(token =>
        {
            var result = FileComparer.CompareSourceFiles(
                input.OldContent, input.NewContent, input.OldTitle, input.NewTitle,
                Settings.IgnoreCase, Settings.Whitespace, Settings.Detection);
            token.ThrowIfCancellationRequested();

            var document = DiffDocumentBuilder.BuildTwoWay(
                result, Settings, Path.GetFileName(input.OldTitle), Path.GetFileName(input.NewTitle));
            return Settings.Layout == PaneLayout.Unified ? DiffDocumentBuilder.ToUnified(document) : document;
        });
    }

    private async Task RunAnalysisAsync(Func<CancellationToken, DiffDocument> analyse)
    {
        _analysis?.Cancel();
        var cancellation = new CancellationTokenSource();
        _analysis = cancellation;

        IsBusy = true;
        try
        {
            var document = await Task.Run(() => analyse(cancellation.Token), cancellation.Token);
            if (cancellation.IsCancellationRequested) return;

            Document = document;
            SelectedRow = -1;
            ApplyFilter();
            StatusText = document.Summary.HasDifferences
                ? $"{document.Summary.Text} · {document.LanguageName}"
                : $"No differences · {document.LanguageName}";
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer analysis; the newer one reports the status.
        }
        catch (Exception exception)
        {
            StatusText = $"Comparison failed: {exception.Message}";
            Document = DiffDocument.Empty;
            ApplyFilter();
        }
        finally
        {
            if (ReferenceEquals(_analysis, cancellation)) IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        VisibleRows = DiffFilter.Apply(Document.Rows, SearchText, Filter);
        Raise(nameof(ChangePositionText));
    }

    private void SelectDocumentRow(int documentRow)
    {
        if (documentRow < 0 || documentRow >= Document.Rows.Count) return;

        var row = Document.Rows[documentRow];
        var visibleIndex = IndexOfVisibleRow(row);
        if (visibleIndex < 0)
        {
            // The row is filtered out; drop the filter so navigation still works.
            SearchText = string.Empty;
            Filter = ChangeTypeFilter.All;
            visibleIndex = IndexOfVisibleRow(row);
        }

        SelectedRow = visibleIndex;
    }

    private int IndexOfVisibleRow(DiffRow row)
    {
        for (var i = 0; i < VisibleRows.Count; i++)
        {
            if (ReferenceEquals(VisibleRows[i], row)) return i;
        }

        return -1;
    }

    private int RowIndexInDocument(int visibleRow)
    {
        if (visibleRow < 0 || visibleRow >= VisibleRows.Count) return -1;

        var row = VisibleRows[visibleRow];
        for (var i = 0; i < Document.Rows.Count; i++)
        {
            if (ReferenceEquals(Document.Rows[i], row)) return i;
        }

        return -1;
    }
}
