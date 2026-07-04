using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class BlockSimilarityScorerTests
{
    [Fact]
    public void ExactHashOverlap_returns_one_for_fully_identical_lines_within_bounds()
    {
        var oldLines = new[]
        {
            "old prefix line differs outside range",
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "old suffix line differs outside range",
        };
        var newLines = new[]
        {
            "new prefix line differs outside range",
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "new suffix line differs outside range",
        };
        var candidate = new BlockCandidate(1, 2, 1, 2);

        var result = BlockSimilarityScorer.ExactHashOverlap(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void ExactHashOverlap_returns_fraction_for_one_different_line_in_the_range()
    {
        var oldLines = new[]
        {
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };
        var newLines = new[]
        {
            "block line Alpha long enough content",
            "changed block line Beta content",
            "block line Gamma long enough content",
        };
        var candidate = new BlockCandidate(0, 2, 0, 2);

        var result = BlockSimilarityScorer.ExactHashOverlap(candidate, oldLines, newLines);

        Assert.Equal(2.0 / 3.0, result);
    }

    [Fact]
    public void ExactHashOverlap_returns_zero_when_every_line_in_the_range_differs()
    {
        var oldLines = new[]
        {
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };
        var newLines = new[]
        {
            "changed line Alpha long enough content",
            "changed line Beta long enough content",
            "changed line Gamma long enough content",
        };
        var candidate = new BlockCandidate(0, 2, 0, 2);

        var result = BlockSimilarityScorer.ExactHashOverlap(candidate, oldLines, newLines);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void ExactHashOverlap_returns_one_for_BlockBuilder_contiguous_moved_block_candidate()
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

        var candidate = Assert.Single(BlockBuilder.Build(oldLines, newLines));
        var result = BlockSimilarityScorer.ExactHashOverlap(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void NormalizedHashOverlap_ignores_internal_whitespace_differences_that_ExactHashOverlap_would_count_as_a_mismatch()
    {
        var oldLines = new[]
        {
            "block line Alpha long enough content",
            "block line   Beta    long enough content",
            "block line Gamma long enough content",
        };
        var newLines = new[]
        {
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };
        var candidate = new BlockCandidate(0, 2, 0, 2);

        var normalized = BlockSimilarityScorer.NormalizedHashOverlap(candidate, oldLines, newLines);
        var exact = BlockSimilarityScorer.ExactHashOverlap(candidate, oldLines, newLines);

        Assert.Equal(1.0, normalized);
        Assert.Equal(2.0 / 3.0, exact);
    }

    [Fact]
    public void NormalizedHashOverlap_still_counts_a_mismatch_when_line_content_actually_differs()
    {
        var oldLines = new[]
        {
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };
        var newLines = new[]
        {
            "block line Alpha long enough content",
            "changed block line Beta content",
            "block line Gamma long enough content",
        };
        var candidate = new BlockCandidate(0, 2, 0, 2);

        var result = BlockSimilarityScorer.NormalizedHashOverlap(candidate, oldLines, newLines);

        Assert.Equal(2.0 / 3.0, result);
    }

    [Fact]
    public void NormalizedHashOverlap_returns_one_for_fully_identical_lines_within_bounds()
    {
        var oldLines = new[]
        {
            "old prefix line differs outside range",
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "old suffix line differs outside range",
        };
        var newLines = new[]
        {
            "new prefix line differs outside range",
            "block line Alpha long enough content",
            "block line Beta long enough content",
            "new suffix line differs outside range",
        };
        var candidate = new BlockCandidate(1, 2, 1, 2);

        var result = BlockSimilarityScorer.NormalizedHashOverlap(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }
}
