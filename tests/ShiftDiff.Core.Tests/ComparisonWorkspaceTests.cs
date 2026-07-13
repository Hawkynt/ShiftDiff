using System.Text;
using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

public class ComparisonWorkspaceTests
{
    private static byte[] Text(params string[] lines) => Encoding.UTF8.GetBytes(string.Join('\n', lines));

    [Fact]
    public void Compare_AlignsAFileMovedBetweenFoldersIntoOneLogicalRow()
    {
        var source = new WorkspaceSource("a", "Folder A", new Dictionary<string, byte[]>
        {
            ["old/engine.cs"] = Text("alpha", "beta", "gamma"),
        });
        var target = new WorkspaceSource("b", "Folder B", new Dictionary<string, byte[]>
        {
            ["new/engine.cs"] = Text("alpha", "beta", "gamma"),
        });

        var result = ComparisonWorkspace.Compare(source, target);

        var row = Assert.Single(result.Rows);
        Assert.Equal("old/engine.cs", row.Cells[0]!.RelativePath);
        Assert.Equal("new/engine.cs", row.Cells[1]!.RelativePath);
        var link = Assert.Single(result.Relationships, relationship => relationship.Kind == WorkspaceRelationshipKind.FileMoved);
        Assert.Equal(("old/engine.cs", "new/engine.cs"), (link.SourcePath, link.TargetPath));
    }

    [Fact]
    public void Compare_InfersFolderMoveWhenAllContainedFilesMovedTogether()
    {
        var source = new WorkspaceSource("a", "Folder A", new Dictionary<string, byte[]>
        {
            ["old/a.cs"] = Text("alpha"),
            ["old/b.cs"] = Text("beta"),
        });
        var target = new WorkspaceSource("b", "Folder B", new Dictionary<string, byte[]>
        {
            ["new/a.cs"] = Text("alpha"),
            ["new/b.cs"] = Text("beta"),
        });

        var result = ComparisonWorkspace.Compare(source, target);

        var folderMove = Assert.Single(result.Relationships, relationship => relationship.Kind == WorkspaceRelationshipKind.FolderMoved);
        Assert.Equal("old", folderMove.SourcePath);
        Assert.Equal("new", folderMove.TargetPath);
    }

    [Fact]
    public void Compare_AlignsFourSourcesAgainstTheCommonBase()
    {
        var content = Text("same");
        var sources = Enumerable.Range(0, 4)
            .Select(index => new WorkspaceSource(index.ToString(), $"Source {index}", new Dictionary<string, byte[]> { ["file.txt"] = content }))
            .ToArray();

        var result = ComparisonWorkspace.Compare(sources);

        var row = Assert.Single(result.Rows);
        Assert.Equal(4, row.Cells.Count);
        Assert.All(row.Cells, cell => Assert.NotNull(cell));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Compare_RejectsUnsupportedPaneCount(int count)
    {
        var sources = Enumerable.Range(0, count)
            .Select(index => new WorkspaceSource(index.ToString(), index.ToString(), new Dictionary<string, byte[]>()))
            .ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => ComparisonWorkspace.Compare(sources));
    }

    [Fact]
    public void Compare_PreservesAddedAndRemovedCellsAsEmptyCounterparts()
    {
        var source = new WorkspaceSource("a", "A", new Dictionary<string, byte[]> { ["removed.txt"] = Text("alpha") });
        var target = new WorkspaceSource("b", "B", new Dictionary<string, byte[]> { ["added.txt"] = Text("one", "two", "three", "four", "five", "six") });

        var result = ComparisonWorkspace.Compare(source, target);

        var removed = Assert.Single(result.Rows, row => row.LogicalPath == "removed.txt");
        Assert.NotNull(removed.Cells[0]);
        Assert.Null(removed.Cells[1]);
        var added = Assert.Single(result.Rows, row => row.LogicalPath == "added.txt");
        Assert.Null(added.Cells[0]);
        Assert.Equal(FolderChangeType.Added, added.Cells[1]!.ChangeType);
    }
}
