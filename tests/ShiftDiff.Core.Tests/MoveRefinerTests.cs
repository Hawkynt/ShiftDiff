using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

public class MoveRefinerTests
{
    [Fact]
    public void Refine_BlocksThatOnlyShiftedDown_ReportsNoMoves()
    {
        // Two lines were inserted at the top; everything below merely slid down.
        BlockMatch[] blocks =
        [
            Block(0, 5, 2, 7),
            Block(6, 10, 8, 12),
        ];

        Assert.Empty(MoveRefiner.Refine(blocks));
    }

    [Fact]
    public void Refine_TwoSwappedBlocks_ReportsTheSmallerOneAsMoved()
    {
        BlockMatch[] blocks =
        [
            Block(0, 19, 30, 49),
            Block(20, 25, 0, 5),
        ];

        var moved = Assert.Single(MoveRefiner.Refine(blocks));

        Assert.Equal(20, moved.OldStart);
        Assert.Equal(0, moved.NewStart);
    }

    [Fact]
    public void Refine_SingleBlock_IsNeverAMove()
    {
        Assert.Empty(MoveRefiner.Refine([Block(0, 10, 40, 50)]));
    }

    [Fact]
    public void Refine_NoBlocks_ReturnsNothing()
    {
        Assert.Empty(MoveRefiner.Refine([]));
    }

    [Fact]
    public void Coalesce_AdjacentBlocksWithTheSameDisplacement_BecomeOneBlock()
    {
        BlockMatch[] blocks =
        [
            Block(0, 3, 5, 8),
            Block(4, 9, 9, 14),
        ];

        var merged = Assert.Single(MoveRefiner.Coalesce(blocks));

        Assert.Equal(0, merged.OldStart);
        Assert.Equal(9, merged.OldEnd);
        Assert.Equal(5, merged.NewStart);
        Assert.Equal(14, merged.NewEnd);
    }

    [Fact]
    public void Coalesce_BlocksSeparatedByOneUnmatchedLine_StillMerge()
    {
        BlockMatch[] blocks =
        [
            Block(0, 3, 5, 8),
            Block(5, 9, 10, 14),
        ];

        Assert.Single(MoveRefiner.Coalesce(blocks));
    }

    [Fact]
    public void Coalesce_BlocksWithDifferentDisplacement_StaySeparate()
    {
        BlockMatch[] blocks =
        [
            Block(0, 3, 5, 8),
            Block(4, 9, 20, 25),
        ];

        Assert.Equal(2, MoveRefiner.Coalesce(blocks).Length);
    }

    [Fact]
    public void Coalesce_BlocksFarApart_StaySeparate()
    {
        BlockMatch[] blocks =
        [
            Block(0, 3, 5, 8),
            Block(40, 45, 45, 50),
        ];

        Assert.Equal(2, MoveRefiner.Coalesce(blocks).Length);
    }

    [Fact]
    public void Coalesce_DifferentMatchTypes_StaySeparate()
    {
        BlockMatch[] blocks =
        [
            Block(0, 3, 5, 8),
            Block(4, 9, 9, 14) with { MatchType = ChangeType.MovedEdited },
        ];

        Assert.Equal(2, MoveRefiner.Coalesce(blocks).Length);
    }

    [Fact]
    public void Coalesce_MergedBlock_KeepsTheStrongerConfidenceAndALengthWeightedScore()
    {
        BlockMatch[] blocks =
        [
            Block(0, 0, 0, 0) with { Score = 1.0, Confidence = Confidence.Likely },
            Block(1, 3, 1, 3) with { Score = 0.5, Confidence = Confidence.Certain },
        ];

        var merged = Assert.Single(MoveRefiner.Coalesce(blocks));

        Assert.Equal(Confidence.Certain, merged.Confidence);
        Assert.Equal(0.625, merged.Score, 3);
    }

    [Fact]
    public void KeepReorderings_PrefersKeepingTheLargestBlockStable()
    {
        BlockMatch[] blocks =
        [
            Block(0, 2, 50, 52),
            Block(10, 60, 0, 50),
        ];

        var moved = Assert.Single(MoveRefiner.KeepReorderings(blocks));

        Assert.Equal(0, moved.OldStart);
    }

    [Fact]
    public void KeepReorderings_ThreeBlocksWithOneOutOfOrder_ReportsOnlyThatOne()
    {
        BlockMatch[] blocks =
        [
            Block(0, 9, 0, 9),
            Block(10, 13, 40, 43),
            Block(20, 39, 10, 29),
        ];

        var moved = Assert.Single(MoveRefiner.KeepReorderings(blocks));

        Assert.Equal(10, moved.OldStart);
    }

    [Fact]
    public void Refine_ManyBlocksInReadingOrder_ReportsNoMoves()
    {
        var blocks = Enumerable.Range(0, 50)
            .Select(i => Block(i * 10, i * 10 + 5, i * 10 + 3, i * 10 + 8))
            .ToArray();

        Assert.Empty(MoveRefiner.Refine(blocks));
    }

    private static BlockMatch Block(int oldStart, int oldEnd, int newStart, int newEnd) =>
        new(oldStart, oldEnd, newStart, newEnd, ChangeType.Moved, 0.9, Confidence.Certain);
}
