using System.Text;
using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class UnifiedDiffBuilderTests
{
    [Fact]
    public void Build_AllUnchanged_ReturnsFileWithNoHunks()
    {
        var changes = new[]
        {
            new LineChange(ChangeType.Unchanged, "a", "a", 0, 0),
            new LineChange(ChangeType.Unchanged, "b", "b", 1, 1),
            new LineChange(ChangeType.Unchanged, "c", "c", 2, 2),
        };

        var file = UnifiedDiffBuilder.Build(changes, "old.txt", "new.txt");

        Assert.Empty(file.Hunks);
        Assert.Equal("old.txt", file.Header.SourcePath);
        Assert.Equal("new.txt", file.Header.TargetPath);
    }

    [Fact]
    public void Build_SingleEditedLineWithSurroundingContext_ProducesOneHunk()
    {
        var changes = new[]
        {
            new LineChange(ChangeType.Unchanged, "a", "a", 0, 0),
            new LineChange(ChangeType.Edited, "b", "B", 1, 1),
            new LineChange(ChangeType.Unchanged, "c", "c", 2, 2),
        };

        var file = UnifiedDiffBuilder.Build(changes, "old.txt", "new.txt", contextLines: 3);

        var hunk = Assert.Single(file.Hunks);
        Assert.Equal(
            new[]
            {
                new UnifiedDiffLine(UnifiedDiffLineKind.Context, "a"),
                new UnifiedDiffLine(UnifiedDiffLineKind.Removed, "b"),
                new UnifiedDiffLine(UnifiedDiffLineKind.Added, "B"),
                new UnifiedDiffLine(UnifiedDiffLineKind.Context, "c"),
            },
            hunk.Lines);
        Assert.Equal(new UnifiedDiffHunkHeader(1, 3, 1, 3), hunk.Header);
    }

    [Fact]
    public void Build_AddedLineAtSpanStart_OldStartAndCountExcludeIt()
    {
        var changes = new[]
        {
            new LineChange(ChangeType.Added, null, "new", null, 0),
            new LineChange(ChangeType.Unchanged, "a", "a", 0, 1),
        };

        var file = UnifiedDiffBuilder.Build(changes, "old.txt", "new.txt");

        var hunk = Assert.Single(file.Hunks);
        Assert.Equal(1, hunk.Header.OldStart);
        Assert.Equal(1, hunk.Header.OldCount);
        Assert.Equal(1, hunk.Header.NewStart);
        Assert.Equal(2, hunk.Header.NewCount);
    }

    [Fact]
    public void Build_EditFarFromArrayStart_StartCountsSkippedLeadingUnchangedLines()
    {
        var changes = new List<LineChange>();
        for (var i = 0; i < 10; i++)
        {
            changes.Add(new LineChange(ChangeType.Unchanged, $"line{i}", $"line{i}", i, i));
        }

        changes.Add(new LineChange(ChangeType.Edited, "old10", "new10", 10, 10));

        var file = UnifiedDiffBuilder.Build(changes, "old.txt", "new.txt", contextLines: 3);

        var hunk = Assert.Single(file.Hunks);
        Assert.Equal(8, hunk.Header.OldStart);
        Assert.Equal(8, hunk.Header.NewStart);
    }

    [Fact]
    public void Build_RemovedLine_ProducesRemovedDiffLine()
    {
        var changes = new[]
        {
            new LineChange(ChangeType.Unchanged, "a", "a", 0, 0),
            new LineChange(ChangeType.Removed, "b", null, 1, null),
            new LineChange(ChangeType.Unchanged, "c", "c", 2, 1),
        };

        var file = UnifiedDiffBuilder.Build(changes, "old.txt", "new.txt");

        var hunk = Assert.Single(file.Hunks);
        Assert.Equal(
            new[]
            {
                new UnifiedDiffLine(UnifiedDiffLineKind.Context, "a"),
                new UnifiedDiffLine(UnifiedDiffLineKind.Removed, "b"),
                new UnifiedDiffLine(UnifiedDiffLineKind.Context, "c"),
            },
            hunk.Lines);
        Assert.Equal(new UnifiedDiffHunkHeader(1, 3, 1, 2), hunk.Header);
    }

    [Fact]
    public void Build_UnsupportedChangeType_ThrowsArgumentOutOfRangeException()
    {
        var changes = new[]
        {
            new LineChange(ChangeType.Moved, "a", "a", 0, 0),
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => UnifiedDiffBuilder.Build(changes, "old.txt", "new.txt"));
    }

    [Fact]
    public void Build_ThenFormatThenParse_RoundTripsThroughRealFileComparerOutput()
    {
        var oldText = "one\ntwo\nthree\nfour\nfive\n";
        var newText = "one\ntwo\nTHREE\nfour\nfive\n";

        var result = FileComparer.Compare(Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText));

        var file = UnifiedDiffBuilder.Build(result.Changes, "old.txt", "new.txt");
        var lines = UnifiedDiffFormatter.Format(file);
        var reparsed = UnifiedDiffParser.ParsePatch(lines.ToList());

        var reparsedFile = Assert.Single(reparsed.Files);
        var expectedHunk = Assert.Single(file.Hunks);
        var actualHunk = Assert.Single(reparsedFile.Hunks);
        Assert.Equal(expectedHunk.Header, actualHunk.Header);
        Assert.Equal(expectedHunk.Lines, actualHunk.Lines);
    }
}
