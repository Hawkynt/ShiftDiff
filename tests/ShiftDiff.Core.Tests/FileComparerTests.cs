using System.Text;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class FileComparerTests
{
    [Fact]
    public void Compare_IdenticalFiles_AllLinesUnchangedNoMovedBlocks()
    {
        var content = Encoding.UTF8.GetBytes("alpha\nbeta\ngamma\n");

        var result = FileComparer.Compare(content, content);

        Assert.All(result.Changes, change => Assert.Equal(ChangeType.Unchanged, change.ChangeType));
        Assert.All(result.Changes, change => Assert.Null(change.TokenChanges));
        Assert.Empty(result.MovedBlocks);
    }

    [Fact]
    public void Compare_SimpleEdit_ReturnsLineLevelChangesNoMovedBlocks()
    {
        var oldContent = Encoding.UTF8.GetBytes("alpha\nbeta\ngamma\n");
        var newContent = Encoding.UTF8.GetBytes("alpha\nBETA\ngamma\n");

        var result = FileComparer.Compare(oldContent, newContent);

        Assert.Contains(result.Changes, change => change.ChangeType == ChangeType.Edited && change.OldLine == "beta" && change.NewLine == "BETA");
        Assert.Empty(result.MovedBlocks);
    }

    [Fact]
    public void Compare_SimpleEdit_TokenChangesIsolateTheChangedWord()
    {
        var oldContent = Encoding.UTF8.GetBytes("alpha\nbeta\ngamma\n");
        var newContent = Encoding.UTF8.GetBytes("alpha\nBETA\ngamma\n");

        var result = FileComparer.Compare(oldContent, newContent);

        var edited = Assert.Single(result.Changes, change => change.ChangeType == ChangeType.Edited);
        Assert.NotNull(edited.TokenChanges);
        Assert.Contains(edited.TokenChanges!, tc => tc.ChangeType != ChangeType.Unchanged && (tc.OldToken == "beta" || tc.NewToken == "BETA"));
    }

    [Fact]
    public void Compare_MovedBlock_ReturnsBothRawLineChangesAndTheBlockMatch()
    {
        var oldLines = new[]
        {
            "filler original line zero content aaa",
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };
        var newLines = new[]
        {
            "filler new line zero content bbb",
            "filler new line one content ccc",
            "filler new line two content ddd",
            "filler new line three content eee",
            "filler new line four content fff",
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };
        var oldContent = Encoding.UTF8.GetBytes(string.Join('\n', oldLines) + "\n");
        var newContent = Encoding.UTF8.GetBytes(string.Join('\n', newLines) + "\n");

        var result = FileComparer.Compare(oldContent, newContent);

        // The block's content is identical on both sides, so LineDiffer's LCS
        // alignment matches it by content (Unchanged at its new position) —
        // it's FileComparer.MovedBlocks, not the raw line Changes, that flags
        // the block as having moved. Both results are independently correct;
        // FileComparer hands over both without merging them.
        Assert.Contains(result.Changes, change => change.ChangeType == ChangeType.Unchanged && change.OldLine == "block line Alpha long enough content" && change.OldIndex == 1 && change.NewIndex == 5);
        var match = Assert.Single(result.MovedBlocks);
        Assert.Equal(new BlockMatch(1, 3, 5, 7, ChangeType.Moved, 0.875, Confidence.Certain), match);
    }

    [Fact]
    public void Compare_WithIgnoreCaseAndWhitespaceMode_ForwardsToLineDiffer()
    {
        var oldContent = Encoding.UTF8.GetBytes("Alpha\n");
        var newContent = Encoding.UTF8.GetBytes("alpha  \n");

        var result = FileComparer.Compare(oldContent, newContent, ignoreCase: true, whitespaceMode: WhitespaceMode.Trim);

        Assert.All(result.Changes, change => Assert.Equal(ChangeType.Unchanged, change.ChangeType));
    }

    [Fact]
    public void Compare_BomAndCrLfInput_UsesTextFileLoaderTransparently()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var oldContent = bom.Concat(Encoding.UTF8.GetBytes("alpha\r\nbeta\r\n")).ToArray();
        var newContent = Encoding.UTF8.GetBytes("alpha\r\nbeta\r\n");

        var result = FileComparer.Compare(oldContent, newContent);

        Assert.All(result.Changes, change => Assert.Equal(ChangeType.Unchanged, change.ChangeType));
        Assert.Empty(result.MovedBlocks);
    }

    private static byte[] TwoLineFile => Encoding.UTF8.GetBytes(string.Join('\n', ["one", "two", string.Empty]));

    [Fact]
    public void Compare_EmptyOldFile_ReportsEveryNewLineAsAdded()
    {
        var result = FileComparer.Compare([], TwoLineFile);

        Assert.Equal(2, result.Changes.Count(change => change.ChangeType == ChangeType.Added));
        Assert.DoesNotContain(result.Changes, change => change.ChangeType == ChangeType.Edited);
    }

    [Fact]
    public void Compare_EmptyNewFile_ReportsEveryOldLineAsRemoved()
    {
        var result = FileComparer.Compare(TwoLineFile, []);

        Assert.Equal(2, result.Changes.Count(change => change.ChangeType == ChangeType.Removed));
    }

    [Fact]
    public void Compare_TwoEmptyFiles_ReportsNoChanges()
    {
        var result = FileComparer.Compare([], []);

        Assert.DoesNotContain(result.Changes, change => change.ChangeType != ChangeType.Unchanged);
    }
}
