using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class BlockClassifierTests
{
    [Fact]
    public void Classify_returns_no_matches_for_no_candidates()
    {
        var result = BlockClassifier.Classify(System.Array.Empty<BlockCandidate>());

        Assert.Empty(result);
    }

    [Fact]
    public void Classify_marks_a_single_candidate_as_moved_with_positions_copied_through()
    {
        var candidates = new[] { new BlockCandidate(1, 3, 5, 7) };

        var result = BlockClassifier.Classify(candidates);

        var match = Assert.Single(result);
        Assert.Equal(new BlockMatch(1, 3, 5, 7, ChangeType.Moved), match);
    }

    [Fact]
    public void Classify_marks_every_candidate_as_moved_and_preserves_order()
    {
        var candidates = new[]
        {
            new BlockCandidate(0, 0, 1, 1),
            new BlockCandidate(2, 2, 3, 3),
        };

        var result = BlockClassifier.Classify(candidates);

        Assert.Equal(2, result.Length);
        Assert.Equal(new BlockMatch(0, 0, 1, 1, ChangeType.Moved), result[0]);
        Assert.Equal(new BlockMatch(2, 2, 3, 3, ChangeType.Moved), result[1]);
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
        var result = BlockClassifier.Classify(candidates);

        var match = Assert.Single(result);
        Assert.Equal(new BlockMatch(1, 3, 5, 7, ChangeType.Moved), match);
    }
}
