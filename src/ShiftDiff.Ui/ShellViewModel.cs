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
    // The current session's analysis, replayed whenever a setting changes — a
    // three- or four-way comparison has no IComparisonSource to fall back on.
    private Func<CancellationToken, DiffDocument>? _reanalyse;
    private readonly HashSet<int> _expandedRegions = [];
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
    private VcsWorkspace? _workspace;
    private string _fromRevision = VcsRevisions.Head;
    private string _toRevision = VcsRevisions.WorkingTree;
    private InteractiveMergeDocument? _merge;
    private int _mergedLineCount;
    private bool _isBusy;
    private bool _showFileList;
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
            Raise(nameof(Links));
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

    public IReadOnlyList<PaneLink> Links => Document.Links;

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

    /// <summary>True for folder and repository sessions, where picking a file is part of the workflow.</summary>
    public bool ShowFileList
    {
        get => _showFileList;
        private set => SetProperty(ref _showFileList, value);
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

            // Hand-expanded regions belong to the file that was open.
            _expandedRegions.Clear();
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

    /// <summary>Two to four folder trees aligned side by side (§7.3 four-way comparison).</summary>
    public Task OpenWorkspaceAsync(IReadOnlyList<string> folders) =>
        OpenAsync(new WorkspaceComparisonSource(folders.Take(4).ToArray()));

    public Task OpenRepositoryAsync(
        string path, string fromRevision = VcsRevisions.Head, string toRevision = VcsRevisions.WorkingTree)
    {
        var workspace = VcsWorkspace.Open(path);
        if (workspace is null)
        {
            StatusText = $"No Git or SVN repository found at {path}";
            return Task.CompletedTask;
        }

        _workspace = workspace;
        FromRevision = fromRevision;
        ToRevision = toRevision;
        Raise(nameof(IsRepositorySession));
        return OpenRevisionRangeAsync(fromRevision, toRevision);
    }

    /// <summary>FR-030/FR-031: recompare the open repository across another revision range.</summary>
    public Task OpenRevisionRangeAsync(string fromRevision, string toRevision)
    {
        if (_workspace is null)
        {
            StatusText = "Open a repository first.";
            return Task.CompletedTask;
        }

        FromRevision = fromRevision;
        ToRevision = toRevision;

        try
        {
            return OpenAsync(new VcsComparisonSource(_workspace, fromRevision, toRevision));
        }
        catch (VcsCommandException exception)
        {
            StatusText = exception.Message;
            return Task.CompletedTask;
        }
    }

    public bool IsRepositorySession => _workspace is not null;

    public string FromRevision
    {
        get => _fromRevision;
        private set => SetProperty(ref _fromRevision, value);
    }

    public string ToRevision
    {
        get => _toRevision;
        private set => SetProperty(ref _toRevision, value);
    }

    /// <summary>Revision history of the open repository, newest first (FR-030).</summary>
    public IReadOnlyList<VcsRevision> History(string? relativePath = null, int limit = 50) =>
        _workspace is null ? [] : _workspace.Provider.GetHistory(_workspace.Root, relativePath, limit);

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
            // Three or four folders is a workspace comparison (§7.3), three or
            // four files is a merge comparison.
            case >= 3 when paths.Take(paths.Count).All(Directory.Exists):
                return OpenWorkspaceAsync(paths);
            case 3:
                return OpenThreeWayAsync(paths[0], paths[1], paths[2]);
            default:
                return OpenFourWayAsync(paths[0], paths[1], paths[2], paths[3]);
        }
    }

    public async Task OpenThreeWayAsync(string basePath, string localPath, string remotePath)
    {
        StartMergeDocument(File.ReadAllLines(localPath));
        await RunAnalysisAsync(token =>
        {
            var changes = ThreeWayComparer.Compare(
                File.ReadAllLines(basePath), File.ReadAllLines(localPath), File.ReadAllLines(remotePath),
                Settings.IgnoreCase, Settings.Whitespace);
            token.ThrowIfCancellationRequested();

            var language = SourceLanguageDetector.Detect(localPath);
            return DiffDocumentBuilder.BuildThreeWay(
                changes, Settings, language,
                Path.GetFileName(basePath), Path.GetFileName(localPath), Path.GetFileName(remotePath),
                _expandedRegions);
        });

        Settings.Layout = PaneLayout.ThreeWay;
        SessionTitle = $"{Path.GetFileName(localPath)} ↔ {Path.GetFileName(remotePath)}";
        _source = null;
        ShowFileList = false;
        FileCollection.Clear();
    }

    public async Task OpenFourWayAsync(string basePath, string localPath, string remotePath, string targetPath)
    {
        DiscardMergeDocument();
        await RunAnalysisAsync(token =>
        {
            var changes = ThreeWayComparer.Compare(
                File.ReadAllLines(basePath), File.ReadAllLines(localPath), File.ReadAllLines(remotePath),
                Settings.IgnoreCase, Settings.Whitespace);
            token.ThrowIfCancellationRequested();

            return DiffDocumentBuilder.BuildFourWay(
                changes, File.ReadAllLines(targetPath), Settings, SourceLanguageDetector.Detect(localPath),
                Path.GetFileName(basePath), Path.GetFileName(localPath),
                Path.GetFileName(remotePath), Path.GetFileName(targetPath),
                _expandedRegions);
        });

        Settings.Layout = PaneLayout.FourWay;
        SessionTitle = $"4-way: {Path.GetFileName(targetPath)}";
        _source = null;
        ShowFileList = false;
        FileCollection.Clear();
    }

    public Task OpenRepositoryOrFolderAsync(string path) =>
        VcsWorkspace.Open(path) is not null
            ? OpenRepositoryAsync(path)
            : Task.FromResult(StatusText = $"Drop a second folder to compare {Path.GetFileName(path)} against.");

    public async Task OpenAsync(IComparisonSource source)
    {
        if (source is not VcsComparisonSource)
        {
            _workspace = null;
            Raise(nameof(IsRepositorySession));
        }

        _source = source;
        ShowFileList = source is not FilePairSource;
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

    public Task RefreshAsync()
    {
        if (_source is not null && SelectedFile is not null) return CompareSelectedFileAsync();
        return _reanalyse is null ? Task.CompletedTask : RunAnalysisAsync(_reanalyse);
    }

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

    /// <summary>Expands one folded region back into its individual lines, leaving the rest folded.</summary>
    public Task ExpandRegionAsync(DiffRow collapsedRow)
    {
        if (collapsedRow.Kind != DiffRowKind.Collapsed) return Task.CompletedTask;
        if (!_expandedRegions.Add(FoldedRegionKey(collapsedRow))) return Task.CompletedTask;

        return RefreshAsync();
    }

    /// <summary>Folds every region that was expanded by hand back up again.</summary>
    public Task CollapseAllRegionsAsync()
    {
        if (_expandedRegions.Count == 0) return Task.CompletedTask;

        _expandedRegions.Clear();
        return RefreshAsync();
    }

    // Keyed by the first hidden line so the choice survives a rebuild of the
    // document. Insertions have no old index, so their new index is used with a
    // disjoint (negative) key space.
    internal static int FoldedRegionKey(DiffRow row) =>
        row.OldIndex is { } oldIndex ? oldIndex : -(row.NewIndex ?? 0) - 1;

    // --- interactive merge (FR-047) ---------------------------------------

    /// <summary>Replaces the selected block in the result with the left pane's version.</summary>
    public bool TakeSelectedBlockFromLeft() => TakeSelectedBlock(0);

    /// <summary>
    /// FR-047: replaces the selected change run in the reconstructed result with
    /// one pane's version. The result mirrors the second pane's line numbering —
    /// the target file in a two-way comparison, the local file in a merge — which
    /// is what DiffRow.NewIndex carries in both cases.
    /// </summary>
    public bool TakeSelectedBlock(int sourcePane)
    {
        var run = SelectedRunInDocument();
        return run is var (start, end) && TakeBlockRange(start, end, sourcePane);
    }

    /// <summary>
    /// Transfers one identified change block, whichever row the cursor is on —
    /// what the in-place arrow on the block does.
    /// </summary>
    public bool TakeBlock(int blockId, int sourcePane)
    {
        var range = RangeOfBlock(blockId);
        return range is var (start, end) && TakeBlockRange(start, end, sourcePane);
    }

    private (int Start, int End)? RangeOfBlock(int blockId)
    {
        int? start = null;
        var end = 0;
        for (var i = 0; i < Document.Rows.Count; i++)
        {
            if (Document.Rows[i].BlockId != blockId) continue;

            start ??= i;
            end = i;
        }

        return start is { } value ? (value, end) : null;
    }

    private bool TakeBlockRange(int start, int end, int sourcePane)
    {
        if (_merge is null) return false;
        if (sourcePane < 0 || sourcePane >= Document.PaneCount) return false;
        if (start < 0 || end >= Document.Rows.Count || end < start) return false;

        var sourceLines = new List<string>();
        int? targetStart = null;
        var targetCount = 0;

        for (var i = start; i <= end; i++)
        {
            var row = Document.Rows[i];
            if (row.Cells.Count <= sourcePane) continue;

            if (row.Cells[sourcePane].State != CellState.Empty && row.Cells[sourcePane].LineNumber is not null)
            {
                sourceLines.Add(row.Cells[sourcePane].Text);
            }

            if (row.NewIndex is not { } targetIndex) continue;

            targetStart ??= targetIndex;
            targetCount++;
        }

        if (targetStart is null && sourceLines.Count == 0) return false;

        var paneName = Document.PaneTitles.Count > sourcePane ? Document.PaneTitles[sourcePane] : "source";
        var block = new MergeSourceBlock(
            paneName, paneName, start, end, sourceLines, Document.Rows[start].DisplayChangeType);

        var insertionPoint = targetStart ?? EstimateInsertionPoint(start);
        if (targetCount > 0) _merge.Replace(block, insertionPoint, targetCount);
        else _merge.Insert(block, insertionPoint);

        MergedLineCount = _merge.Lines.Count;
        StatusText = $"Took {sourceLines.Count} line(s) from {paneName} into the result";
        Raise(nameof(CanUndoMerge));
        return true;
    }

    /// <summary>True once a session has a reconstructed result that can be edited and saved.</summary>
    public bool CanResolve => _merge is not null;

    public bool UndoMerge()
    {
        if (_merge?.Undo() != true) return false;

        MergedLineCount = _merge.Lines.Count;
        StatusText = "Reverted the last merge action";
        Raise(nameof(CanUndoMerge));
        return true;
    }

    public bool CanUndoMerge => _merge?.CanUndo == true;

    public int MergedLineCount
    {
        get => _mergedLineCount;
        private set => SetProperty(ref _mergedLineCount, value);
    }

    /// <summary>AC-010: never overwrites an existing file unless the caller insists.</summary>
    public bool SaveMergedResult(string path, bool overwrite = false)
    {
        if (_merge is null) return false;
        if (File.Exists(path) && !overwrite)
        {
            StatusText = $"{Path.GetFileName(path)} already exists — confirm the overwrite first";
            return false;
        }

        File.WriteAllLines(path, _merge.Lines);
        StatusText = $"Wrote {_merge.Lines.Count} line(s) to {path}";
        return true;
    }

    public IReadOnlyList<string> MergedLines => _merge?.Lines ?? [];

    private void StartMergeDocument(ComparisonInput input) =>
        StartMergeDocument(TextFileLoader.Load(input.NewContent).Lines);

    // The result always mirrors the second pane: the target file of a two-way
    // comparison, the local file of a merge. Everything else is "take that
    // version instead".
    private void StartMergeDocument(IReadOnlyList<string> targetLines)
    {
        _merge = new InteractiveMergeDocument(targetLines);
        MergedLineCount = _merge.Lines.Count;
        Raise(nameof(CanUndoMerge));
        Raise(nameof(CanResolve));
    }

    private void DiscardMergeDocument()
    {
        _merge = null;
        MergedLineCount = 0;
        Raise(nameof(CanUndoMerge));
        Raise(nameof(CanResolve));
    }

    // The run of consecutive changed rows the cursor sits in.
    private (int Start, int End)? SelectedRunInDocument()
    {
        var index = RowIndexInDocument(SelectedRow);
        if (index < 0 || index >= Document.Rows.Count || !Document.Rows[index].IsChanged) return null;

        var start = index;
        while (start > 0 && Document.Rows[start - 1].IsChanged) start--;

        var end = index;
        while (end + 1 < Document.Rows.Count && Document.Rows[end + 1].IsChanged) end++;

        return (start, end);
    }

    private int EstimateInsertionPoint(int rowIndex)
    {
        for (var i = rowIndex; i >= 0; i--)
        {
            if (Document.Rows[i].NewIndex is { } index) return index + 1;
        }

        return 0;
    }

    // --- internals --------------------------------------------------------

    private async Task CompareSelectedFileAsync()
    {
        if (_source is null || SelectedFile is not { } entry) return;

        var input = _source.Load(entry);
        OldTitle = input.OldTitle;
        NewTitle = input.NewTitle;
        StartMergeDocument(input);

        await RunAnalysisAsync(token =>
        {
            var result = FileComparer.CompareSourceFiles(
                input.OldContent, input.NewContent, input.OldTitle, input.NewTitle,
                Settings.IgnoreCase, Settings.Whitespace, Settings.Detection);
            token.ThrowIfCancellationRequested();

            var document = DiffDocumentBuilder.BuildTwoWay(
                result, Settings, Path.GetFileName(input.OldTitle), Path.GetFileName(input.NewTitle),
                _expandedRegions);
            return Settings.Layout == PaneLayout.Unified ? DiffDocumentBuilder.ToUnified(document) : document;
        });
    }

    private async Task RunAnalysisAsync(Func<CancellationToken, DiffDocument> analyse)
    {
        _reanalyse = analyse;
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
