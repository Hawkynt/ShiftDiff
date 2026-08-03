using System.Text;
using ShiftDiff.Core;
using ShiftDiff.Ui;

namespace ShiftDiff.Ui.Tests;

// Araxis-style change blocks: each run of changed rows is one identified block
// that a pane can box and offer to transfer, and that the gutter can link.
public class ChangeBlockTests
{
    private readonly ComparisonSettings _settings = new() { CollapseUnchanged = false };

    [Fact]
    public void SingleChangedLine_IsABlockOfItsOwn()
    {
        var document = Build("one\ntwo\nthree\n", "one\nTWO\nthree\n");

        var row = Assert.Single(document.Rows, candidate => candidate.IsChanged);
        Assert.Equal(0, row.BlockId);
        Assert.Equal(BlockEdge.Single, row.Edge);
    }

    [Fact]
    public void ConsecutiveChangedLines_ShareOneBlockWithFirstAndLastEdges()
    {
        var document = Build("a\nb\nc\nd\n", "a\nB\nC\nd\n");

        var changed = document.Rows.Where(row => row.IsChanged).ToArray();
        Assert.Equal(2, changed.Length);
        Assert.All(changed, row => Assert.Equal(0, row.BlockId));
        Assert.Equal(BlockEdge.First, changed[0].Edge);
        Assert.Equal(BlockEdge.Last, changed[1].Edge);
    }

    [Fact]
    public void ThreeConsecutiveChangedLines_HaveAMiddleEdge()
    {
        var document = Build("a\nb\nc\nd\ne\n", "a\nB\nC\nD\ne\n");

        var changed = document.Rows.Where(row => row.IsChanged).ToArray();
        Assert.Equal(BlockEdge.Middle, changed[1].Edge);
    }

    [Fact]
    public void SeparateChangeRuns_GetDistinctBlockIds()
    {
        var document = Build("a\nb\nc\nd\ne\n", "a\nB\nc\nD\ne\n");

        var ids = document.Rows.Where(row => row.IsChanged).Select(row => row.BlockId).Distinct().ToArray();
        Assert.Equal(2, ids.Length);
    }

    [Fact]
    public void UnchangedRows_BelongToNoBlock()
    {
        var document = Build("a\nb\n", "a\nB\n");

        Assert.All(
            document.Rows.Where(row => !row.IsChanged),
            row =>
            {
                Assert.Null(row.BlockId);
                Assert.Equal(BlockEdge.None, row.Edge);
            });
    }

    [Fact]
    public void EveryCell_KnowsItsPaneAndWhetherItIsTheLastOne()
    {
        var document = Build("a\n", "b\n");

        var row = document.Rows[0];
        Assert.Equal(0, row.Cells[0].PaneIndex);
        Assert.Equal(1, row.Cells[1].PaneIndex);
        Assert.False(row.Cells[0].IsLastPane);
        Assert.True(row.Cells[1].IsLastPane);
    }

    [Fact]
    public void TransferArrow_SitsOnTheFirstRowOfABlockInTheSourcePaneOnly()
    {
        var document = Build("a\nb\nc\nd\n", "a\nB\nC\nd\n");

        var changed = document.Rows.Where(row => row.IsChanged).ToArray();
        Assert.True(changed[0].Cells[0].CanTransfer);
        Assert.False(changed[1].Cells[0].CanTransfer);

        // The right pane is the reconstructed result; nothing is transferred into itself.
        Assert.All(changed, row => Assert.False(row.Cells[1].CanTransfer));
    }

    [Fact]
    public void TransferArrow_PointsTowardsTheResultPane()
    {
        var changes = ThreeWayComparer.Compare(["one", "two"], ["one", "LOCAL"], ["one", "remote"]);
        var document = DiffDocumentBuilder.BuildThreeWay(changes, _settings);

        var conflict = Assert.Single(document.Rows, row => row.IsChanged);
        Assert.Equal("▶", conflict.Cells[0].TransferGlyph);
        Assert.Equal("◀", conflict.Cells[2].TransferGlyph);
        Assert.True(conflict.Cells[0].CanTransfer);
        Assert.True(conflict.Cells[2].CanTransfer);
        Assert.False(conflict.Cells[1].CanTransfer);
    }

    [Fact]
    // Taking the empty side of an insertion is how the added lines are dropped.
    public void TransferArrow_OnTheEmptySideOfAnInsertion_OffersToDropTheBlock()
    {
        var document = Build("a\n", "a\nadded\n");

        var added = Assert.Single(document.Rows, row => row.ChangeType == ChangeType.Added);
        Assert.True(added.Cells[0].CanTransfer);
        Assert.Contains("Drop", added.Cells[0].TransferTip);
    }

    [Fact]
    public void TransferTip_OnASideWithContent_OffersToUseIt()
    {
        var document = Build("a\nb\n", "a\nB\n");

        var changed = Assert.Single(document.Rows, row => row.IsChanged);
        Assert.Contains("Use", changed.Cells[0].TransferTip);
    }

    [Fact]
    public void FourWayComparison_OffersNoTransferArrows()
    {
        var changes = ThreeWayComparer.Compare(["one", "two"], ["one", "LOCAL"], ["one", "remote"]);
        var document = DiffDocumentBuilder.BuildFourWay(changes, ["one", "LOCAL"], _settings);

        Assert.All(document.Rows, row => Assert.All(row.Cells, cell => Assert.False(cell.CanTransfer)));
    }

    [Fact]
    public void EachChangeBlock_GetsARibbonBetweenNeighbouringPanes()
    {
        var document = Build("a\nb\nc\nd\ne\n", "a\nB\nc\nD\ne\n");

        var ribbons = document.Links.Where(link => !link.IsRelocation).ToArray();
        Assert.Equal(2, ribbons.Length);
        Assert.All(ribbons, link =>
        {
            Assert.Equal(0, link.SourcePane);
            Assert.Equal(1, link.TargetPane);
        });
    }

    [Fact]
    public void AlignedBlock_ProducesAStraightRibbon()
    {
        var document = Build("a\nb\nc\n", "a\nB\nc\n");

        var ribbon = Assert.Single(document.Links);
        Assert.Equal(ribbon.SourceStartRow, ribbon.TargetStartRow);
        Assert.Equal(ribbon.SourceEndRow, ribbon.TargetEndRow);
    }

    [Fact]
    public void InsertionOnlyBlock_StillLinksBothSidesAtTheInsertionPoint()
    {
        var document = Build("a\nc\n", "a\nb\nc\n");

        var ribbon = Assert.Single(document.Links);
        Assert.Equal(ChangeType.Added, ribbon.Kind);
        Assert.True(ribbon.TargetStartRow >= 0 && ribbon.SourceStartRow >= 0);
    }

    [Fact]
    public void ThreeWayComparison_LinksEachNeighbouringPair()
    {
        var changes = ThreeWayComparer.Compare(["one", "two"], ["one", "LOCAL"], ["one", "remote"]);
        var document = DiffDocumentBuilder.BuildThreeWay(changes, _settings);

        Assert.Contains(document.Links, link => link is { SourcePane: 0, TargetPane: 1 });
        Assert.Contains(document.Links, link => link is { SourcePane: 1, TargetPane: 2 });
    }

    [Fact]
    public void IdenticalFiles_HaveNoRibbons()
    {
        Assert.Empty(Build("a\nb\n", "a\nb\n").Links);
    }

    [Fact]
    public void TakeBlock_TransfersThatBlockWhicheverRowIsSelected()
    {
        var shell = new ShellViewModel(_settings);
        shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "a\nb\nc\nd\n", "a\nB\nC\nd\n")).Wait();

        var blockId = shell.Document.Rows.First(row => row.IsChanged).BlockId!.Value;
        Assert.True(shell.TakeBlock(blockId, 0));
        Assert.Equal(["a", "b", "c", "d"], shell.MergedLines);
    }

    [Fact]
    public void TakeBlock_WithAnUnknownBlockId_DoesNothing()
    {
        var shell = new ShellViewModel(_settings);
        shell.OpenAsync(InMemoryComparisonSource.FromText("f.txt", "a\n", "b\n")).Wait();

        Assert.False(shell.TakeBlock(99, 0));
    }

    // A relocated block is bracketed once at each end, not once per line and
    // again for the block as a whole.
    [Fact]
    public void RelocatedBlock_ProducesExactlyOneConnector()
    {
        var document = Build(ReorderedOld, ReorderedNew, DetectionMode.Aggressive);

        Assert.Single(document.MovedBlocks);
        Assert.Single(document.Links);
        Assert.True(document.Links[0].IsRelocation);
    }

    [Fact]
    public void RelocatedBlock_ConnectorSpansEveryRowOfTheBlockOnBothSides()
    {
        var document = Build(ReorderedOld, ReorderedNew, DetectionMode.Aggressive);

        var link = document.Links[0];
        var leftRows = document.Rows
            .Select((row, index) => (row, index))
            .Where(pair => pair.row.MovedBlockId is not null && pair.row.Cells[0].State != CellState.Empty)
            .Select(pair => pair.index)
            .ToArray();
        var rightRows = document.Rows
            .Select((row, index) => (row, index))
            .Where(pair => pair.row.MovedBlockId is not null && pair.row.Cells[1].State != CellState.Empty)
            .Select(pair => pair.index)
            .ToArray();

        Assert.Equal(leftRows.Min(), link.SourceStartRow);
        Assert.Equal(leftRows.Max(), link.SourceEndRow);
        Assert.Equal(rightRows.Min(), link.TargetStartRow);
        Assert.Equal(rightRows.Max(), link.TargetEndRow);
        Assert.True(link.SourceEndRow > link.SourceStartRow, "the bracket spans several lines");
    }

    [Fact]
    public void MultiLineBlock_IsOneConnectorSpanningAllOfItsRows()
    {
        var document = Build(
            string.Join('\n', ["a", "b", "c", "d", "e", string.Empty]),
            string.Join('\n', ["a", "B", "C", "D", "e", string.Empty]));

        var link = Assert.Single(document.Links);
        Assert.Equal(3, link.SourceEndRow - link.SourceStartRow + 1);
        Assert.Equal(3, link.TargetEndRow - link.TargetStartRow + 1);
    }

    private DiffDocument Build(string oldText, string newText, DetectionMode mode)
    {
        var result = FileComparer.CompareSourceFiles(
            Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText), "old.cs", "new.cs", mode: mode);
        return DiffDocumentBuilder.BuildTwoWay(result, _settings);
    }

    private const string ReorderedOld = """
        public class Sample
        {
            public bool Validate(int value)
            {
                if (value < 0)
                {
                    return false;
                }

                return true;
            }

            public string Describe()
            {
                return "sample";
            }
        }
        """;

    private const string ReorderedNew = """
        public class Sample
        {
            public string Describe()
            {
                return "sample";
            }

            public bool Validate(int value)
            {
                if (value < 0)
                {
                    return false;
                }

                return true;
            }
        }
        """;

    private DiffDocument Build(string oldText, string newText)
    {
        var result = FileComparer.CompareSourceFiles(
            Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText), "old.txt", "new.txt");
        return DiffDocumentBuilder.BuildTwoWay(result, _settings);
    }
}
