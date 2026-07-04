using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class BlockClassifierTests
{
    [Fact]
    public void Classify_returns_no_matches_for_no_candidates()
    {
        var result = BlockClassifier.Classify(
            System.Array.Empty<BlockCandidate>(),
            System.Array.Empty<string>(),
            System.Array.Empty<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void Classify_marks_a_single_candidate_as_moved_with_positions_copied_through()
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
        var candidates = new[] { new BlockCandidate(1, 3, 5, 7) };

        var result = BlockClassifier.Classify(candidates, oldLines, newLines);

        var match = Assert.Single(result);
        Assert.Equal(new BlockMatch(1, 3, 5, 7, ChangeType.Moved, 0.875), match);
    }

    [Fact]
    public void Classify_marks_every_candidate_as_moved_and_preserves_order()
    {
        var oldLines = new[]
        {
            "alpha line content unique zero",
            "bravo line content unique one",
            "charlie line content unique two",
        };
        var newLines = new[]
        {
            "delta line content unique zero",
            "alpha line content unique zero",
            "echo line content unique two",
            "charlie line content unique two",
        };
        var candidates = new[]
        {
            new BlockCandidate(0, 0, 1, 1),
            new BlockCandidate(2, 2, 3, 3),
        };

        var result = BlockClassifier.Classify(candidates, oldLines, newLines);

        Assert.Equal(2, result.Length);
        Assert.Equal(0, result[0].OldStart);
        Assert.Equal(0, result[0].OldEnd);
        Assert.Equal(1, result[0].NewStart);
        Assert.Equal(1, result[0].NewEnd);
        Assert.Equal(ChangeType.Moved, result[0].MatchType);
        Assert.Equal(2, result[1].OldStart);
        Assert.Equal(2, result[1].OldEnd);
        Assert.Equal(3, result[1].NewStart);
        Assert.Equal(3, result[1].NewEnd);
        Assert.Equal(ChangeType.Moved, result[1].MatchType);
    }

    [Fact]
    public void Classify_composes_with_BlockBuilder_output_for_a_contiguous_moved_block()
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

        var candidates = BlockBuilder.Build(oldLines, newLines);
        var result = BlockClassifier.Classify(candidates, oldLines, newLines);

        var match = Assert.Single(result);
        Assert.Equal(new BlockMatch(1, 3, 5, 7, ChangeType.Moved, 0.875), match);
    }
}
