using System.Linq;
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

    [Fact]
    public void TokenShingleSimilarity_returns_one_for_identical_content()
    {
        var oldLines = new[]
        {
            "foo bar baz qux",
        };
        var newLines = new[]
        {
            "foo bar baz qux",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.TokenShingleSimilarity(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void TokenShingleSimilarity_returns_zero_for_disjoint_content()
    {
        var oldLines = new[]
        {
            "foo bar baz qux",
        };
        var newLines = new[]
        {
            "zeta eta theta iota",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.TokenShingleSimilarity(candidate, oldLines, newLines);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void TokenShingleSimilarity_returns_exact_fraction_for_partial_overlap()
    {
        var oldLines = new[]
        {
            "alpha beta gamma delta epsilon",
        };
        var newLines = new[]
        {
            "beta gamma delta epsilon zeta",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.TokenShingleSimilarity(candidate, oldLines, newLines);

        Assert.Equal(0.5, result);
    }

    [Fact]
    public void TokenShingleSimilarity_treats_reordered_tokens_as_different()
    {
        var oldLines = new[]
        {
            "alpha beta gamma delta",
        };
        var newLines = new[]
        {
            "delta gamma beta alpha",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.TokenShingleSimilarity(candidate, oldLines, newLines);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void TokenShingleSimilarity_uses_whole_sequence_fallback_for_short_blocks()
    {
        var identicalOldLines = new[]
        {
            "foo bar",
        };
        var identicalNewLines = new[]
        {
            "foo bar",
        };
        var disjointOldLines = new[]
        {
            "foo bar",
        };
        var disjointNewLines = new[]
        {
            "baz qux",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var identicalResult = BlockSimilarityScorer.TokenShingleSimilarity(candidate, identicalOldLines, identicalNewLines);
        var disjointResult = BlockSimilarityScorer.TokenShingleSimilarity(candidate, disjointOldLines, disjointNewLines);

        Assert.Equal(1.0, identicalResult);
        Assert.Equal(0.0, disjointResult);
    }

    [Fact]
    public void TokenShingleSimilarity_returns_one_when_both_sides_have_no_tokens()
    {
        var oldLines = new[]
        {
            "   !!!",
        };
        var newLines = new[]
        {
            "\t---",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.TokenShingleSimilarity(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void SimHashSimilarity_returns_one_for_identical_content()
    {
        var oldLines = new[]
        {
            "alpha beta gamma delta epsilon",
        };
        var newLines = new[]
        {
            "alpha beta gamma delta epsilon",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.SimHashSimilarity(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void SimHashSimilarity_returns_one_when_both_sides_have_no_tokens()
    {
        var oldLines = new[]
        {
            "   !!!",
        };
        var newLines = new[]
        {
            "\t---",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.SimHashSimilarity(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void SimHashSimilarity_returns_zero_when_only_one_side_has_tokens()
    {
        var oldLines = new[]
        {
            "foo bar baz",
        };
        var newLines = new[]
        {
            "   !!!",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.SimHashSimilarity(candidate, oldLines, newLines);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void SimHashSimilarity_returns_golden_value_for_partial_overlap()
    {
        var oldLines = new[]
        {
            "alpha beta gamma delta epsilon zeta eta theta",
        };
        var newLines = new[]
        {
            "alpha beta iota kappa lambda mu nu xi",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.SimHashSimilarity(candidate, oldLines, newLines);

        Assert.Equal(0.515625, result);
    }

    [Fact]
    public void SimHashSimilarity_returns_golden_value_for_disjoint_content()
    {
        var oldLines = new[]
        {
            "foo bar baz qux",
        };
        var newLines = new[]
        {
            "zeta eta theta iota",
        };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.SimHashSimilarity(candidate, oldLines, newLines);

        Assert.Equal(0.546875, result);
    }

    [Fact]
    public void BlockSizeRatio_returns_one_for_equal_size_ranges()
    {
        var oldLines = new[] { "a", "b", "c", "d" };
        var newLines = new[] { "w", "x", "y", "z" };
        var candidate = new BlockCandidate(0, 3, 0, 3);

        var result = BlockSimilarityScorer.BlockSizeRatio(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void BlockSizeRatio_returns_min_over_max_when_old_range_is_bigger()
    {
        var oldLines = new[] { "a", "b", "c", "d", "e" };
        var newLines = new[] { "w", "x" };
        var candidate = new BlockCandidate(0, 4, 0, 1);

        var result = BlockSimilarityScorer.BlockSizeRatio(candidate, oldLines, newLines);

        Assert.Equal(0.4, result);
    }

    [Fact]
    public void BlockSizeRatio_returns_min_over_max_when_new_range_is_bigger()
    {
        var oldLines = new[] { "a", "b" };
        var newLines = new[] { "w", "x", "y", "z", "v" };
        var candidate = new BlockCandidate(0, 1, 0, 4);

        var result = BlockSimilarityScorer.BlockSizeRatio(candidate, oldLines, newLines);

        Assert.Equal(0.4, result);
    }

    [Fact]
    public void BlockSizeRatio_returns_one_for_single_line_both_sides()
    {
        var oldLines = new[] { "a" };
        var newLines = new[] { "w" };
        var candidate = new BlockCandidate(0, 0, 0, 0);

        var result = BlockSimilarityScorer.BlockSizeRatio(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void OrderingConsistency_returns_one_for_same_relative_order()
    {
        var oldLines = new[] { "alpha", "beta", "gamma" };
        var newLines = new[] { "alpha", "beta", "gamma" };
        var candidate = new BlockCandidate(0, 2, 0, 2);

        var result = BlockSimilarityScorer.OrderingConsistency(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void OrderingConsistency_returns_zero_for_fully_reversed_order()
    {
        var oldLines = new[] { "alpha", "beta", "gamma" };
        var newLines = new[] { "gamma", "beta", "alpha" };
        var candidate = new BlockCandidate(0, 2, 0, 2);

        var result = BlockSimilarityScorer.OrderingConsistency(candidate, oldLines, newLines);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void OrderingConsistency_returns_exact_fraction_for_partial_reorder()
    {
        var oldLines = new[] { "alpha", "beta", "gamma", "delta" };
        var newLines = new[] { "alpha", "gamma", "beta", "delta" };
        var candidate = new BlockCandidate(0, 3, 0, 3);

        var result = BlockSimilarityScorer.OrderingConsistency(candidate, oldLines, newLines);

        Assert.Equal(5.0 / 6.0, result);
    }

    [Fact]
    public void OrderingConsistency_returns_one_when_fewer_than_two_unambiguous_matches_remain()
    {
        var oldLines = new[] { "dup", "dup" };
        var newLines = new[] { "dup", "dup" };
        var candidate = new BlockCandidate(0, 1, 0, 1);

        var result = BlockSimilarityScorer.OrderingConsistency(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void OrderingConsistency_excludes_only_the_duplicated_line_and_scores_the_rest()
    {
        var oldLines = new[] { "A", "B", "C", "D", "B" };
        var newLines = new[] { "D", "C", "B", "A" };
        var candidate = new BlockCandidate(0, 4, 0, 3);

        var result = BlockSimilarityScorer.OrderingConsistency(candidate, oldLines, newLines);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void RarityWeightedAnchorScore_returns_one_when_both_sides_all_strong()
    {
        var oldLines = new[] { "block line Alpha long enough", "block line Beta long enough content" };
        var newLines = new[] { "block line Alpha long enough", "block line Beta long enough content" };
        var candidate = new BlockCandidate(0, 1, 0, 1);

        var result = BlockSimilarityScorer.RarityWeightedAnchorScore(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void RarityWeightedAnchorScore_returns_zero_when_both_sides_all_rejected()
    {
        var oldLines = new[] { "", "{", "}", "else" };
        var newLines = new[] { "", "{", "}", "else" };
        var candidate = new BlockCandidate(0, 3, 0, 3);

        var result = BlockSimilarityScorer.RarityWeightedAnchorScore(candidate, oldLines, newLines);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void RarityWeightedAnchorScore_returns_average_of_each_sides_strong_fraction()
    {
        var oldLines = new[]
        {
            "unique enough alpha line content",
            "short",
            "repeated boilerplate line",
            "repeated boilerplate line",
        };
        var newLines = new[]
        {
            "another unique new content line",
            "second unique new content line",
            "",
            "else",
        };
        var candidate = new BlockCandidate(0, 3, 0, 3);

        var result = BlockSimilarityScorer.RarityWeightedAnchorScore(candidate, oldLines, newLines);

        Assert.Equal(0.375, result);
    }

    [Fact]
    public void RarityWeightedAnchorScore_downgrades_a_line_duplicated_elsewhere_in_the_whole_file()
    {
        var oldLines = new[] { "repeated far away line content", "repeated far away line content" };
        var newLines = new[] { "totally distinct unique content line" };
        var candidate = new BlockCandidate(1, 1, 0, 0);

        var result = BlockSimilarityScorer.RarityWeightedAnchorScore(candidate, oldLines, newLines);

        Assert.Equal(0.5, result);
    }

    [Fact]
    public void NeighboringBlockConsistency_returns_one_when_both_neighbors_match()
    {
        var oldLines = new[] { "before line", "A", "B", "after line" };
        var newLines = new[] { "before line", "A", "B", "after line" };
        var candidate = new BlockCandidate(1, 2, 1, 2);

        var result = BlockSimilarityScorer.NeighboringBlockConsistency(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void NeighboringBlockConsistency_returns_zero_when_both_neighbors_differ()
    {
        var oldLines = new[] { "old before", "A", "B", "old after" };
        var newLines = new[] { "new before", "A", "B", "new after" };
        var candidate = new BlockCandidate(1, 2, 1, 2);

        var result = BlockSimilarityScorer.NeighboringBlockConsistency(candidate, oldLines, newLines);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void NeighboringBlockConsistency_excludes_a_missing_neighbor_instead_of_penalizing_it()
    {
        var oldLines = new[] { "A", "B", "after line" };
        var newLines = new[] { "A", "B", "after line" };
        var candidate = new BlockCandidate(0, 1, 0, 1);

        var result = BlockSimilarityScorer.NeighboringBlockConsistency(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void NeighboringBlockConsistency_averages_a_mixed_before_after_match()
    {
        var oldLines = new[] { "match before", "A", "B", "mismatch after old" };
        var newLines = new[] { "match before", "A", "B", "mismatch after new" };
        var candidate = new BlockCandidate(1, 2, 1, 2);

        var result = BlockSimilarityScorer.NeighboringBlockConsistency(candidate, oldLines, newLines);

        Assert.Equal(0.5, result);
    }

    [Fact]
    public void NeighboringBlockConsistency_returns_one_when_candidate_spans_the_whole_file()
    {
        var oldLines = new[] { "A", "B" };
        var newLines = new[] { "A", "B" };
        var candidate = new BlockCandidate(0, 1, 0, 1);

        var result = BlockSimilarityScorer.NeighboringBlockConsistency(candidate, oldLines, newLines);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void RarityWeightedAnchorScore_WithPrecomputedAnchors_MatchesTheScanningOverload()
    {
        var oldLines = new[]
        {
            "unique enough alpha line content",
            "short",
            "repeated boilerplate line",
            "repeated boilerplate line",
        };
        var newLines = new[]
        {
            "another unique new content line",
            "second unique new content line",
            "",
            "else",
        };
        var candidate = new BlockCandidate(0, 3, 0, 3);
        var oldAnchors = AnchorDetector.Detect(oldLines);
        var newAnchors = AnchorDetector.Detect(newLines);

        var expected = BlockSimilarityScorer.RarityWeightedAnchorScore(candidate, oldLines, newLines);
        var actual = BlockSimilarityScorer.RarityWeightedAnchorScore(candidate, oldAnchors, newAnchors);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CombinedScore_WithPrecomputedAnchors_MatchesTheScanningOverload()
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
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };
        var candidate = new BlockCandidate(0, 2, 0, 2);
        var oldAnchors = AnchorDetector.Detect(oldLines);
        var newAnchors = AnchorDetector.Detect(newLines);

        var expected = BlockSimilarityScorer.CombinedScore(candidate, oldLines, newLines);
        var actual = BlockSimilarityScorer.CombinedScore(candidate, oldLines, newLines, oldAnchors, newAnchors);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExactHashOverlap_WithPrecomputedHashes_MatchesTheScanningOverload()
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
        var oldHashesRaw = oldLines.Select(LineHasher.HashRaw).ToArray();
        var newHashesRaw = newLines.Select(LineHasher.HashRaw).ToArray();

        var expected = BlockSimilarityScorer.ExactHashOverlap(candidate, oldLines, newLines);
        var actual = BlockSimilarityScorer.ExactHashOverlapFromHashes(candidate, oldHashesRaw, newHashesRaw);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizedHashOverlap_WithPrecomputedHashes_MatchesTheScanningOverload()
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
        var oldHashesNormalized = oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();
        var newHashesNormalized = newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();

        var expected = BlockSimilarityScorer.NormalizedHashOverlap(candidate, oldLines, newLines);
        var actual = BlockSimilarityScorer.NormalizedHashOverlapFromHashes(candidate, oldHashesNormalized, newHashesNormalized);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderingConsistency_WithPrecomputedHashes_MatchesTheScanningOverload()
    {
        var oldLines = new[] { "alpha", "beta", "gamma", "delta" };
        var newLines = new[] { "alpha", "gamma", "beta", "delta" };
        var candidate = new BlockCandidate(0, 3, 0, 3);
        var oldHashesNormalized = oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();
        var newHashesNormalized = newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();

        var expected = BlockSimilarityScorer.OrderingConsistency(candidate, oldLines, newLines);
        var actual = BlockSimilarityScorer.OrderingConsistencyFromHashes(candidate, oldHashesNormalized, newHashesNormalized);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NeighboringBlockConsistency_WithPrecomputedHashes_MatchesTheScanningOverload()
    {
        var oldLines = new[] { "match before", "A", "B", "mismatch after old" };
        var newLines = new[] { "match before", "A", "B", "mismatch after new" };
        var candidate = new BlockCandidate(1, 2, 1, 2);
        var oldHashesNormalized = oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();
        var newHashesNormalized = newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();

        var expected = BlockSimilarityScorer.NeighboringBlockConsistency(candidate, oldLines, newLines);
        var actual = BlockSimilarityScorer.NeighboringBlockConsistencyFromHashes(candidate, oldHashesNormalized, newHashesNormalized);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CombinedScore_WithPrecomputedHashes_MatchesTheScanningOverload()
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
            "block line Beta long enough content",
            "block line Gamma long enough content",
        };
        var candidate = new BlockCandidate(0, 2, 0, 2);
        var oldAnchors = AnchorDetector.Detect(oldLines);
        var newAnchors = AnchorDetector.Detect(newLines);
        var oldHashesRaw = oldLines.Select(LineHasher.HashRaw).ToArray();
        var newHashesRaw = newLines.Select(LineHasher.HashRaw).ToArray();
        var oldHashesNormalized = oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();
        var newHashesNormalized = newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();

        var expected = BlockSimilarityScorer.CombinedScore(candidate, oldLines, newLines);
        var actual = BlockSimilarityScorer.CombinedScore(
            candidate,
            oldLines,
            newLines,
            oldHashesRaw,
            newHashesRaw,
            oldHashesNormalized,
            newHashesNormalized,
            oldAnchors,
            newAnchors);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TokenShingleSimilarityFromFingerprint_MatchesConvenienceOverload_ForWholeFileSpan()
    {
        var oldLines = new[] { "block line Alpha long enough content", "block line Beta long enough content" };
        var newLines = new[] { "block line Alpha long enough content", "block line CHANGED long enough content" };
        var candidate = new BlockCandidate(0, oldLines.Length - 1, 0, newLines.Length - 1);

        var expected = BlockSimilarityScorer.TokenShingleSimilarity(candidate, oldLines, newLines);
        var oldFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(oldLines);
        var newFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(newLines);
        var actual = BlockSimilarityScorer.TokenShingleSimilarityFromFingerprint(oldFingerprint, newFingerprint);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SimHashSimilarityFromFingerprint_MatchesConvenienceOverload_ForWholeFileSpan()
    {
        var oldLines = new[] { "block line Alpha long enough content", "block line Beta long enough content" };
        var newLines = new[] { "block line Alpha long enough content", "block line CHANGED long enough content" };
        var candidate = new BlockCandidate(0, oldLines.Length - 1, 0, newLines.Length - 1);

        var expected = BlockSimilarityScorer.SimHashSimilarity(candidate, oldLines, newLines);
        var oldFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(oldLines);
        var newFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(newLines);
        var actual = BlockSimilarityScorer.SimHashSimilarityFromFingerprint(oldFingerprint, newFingerprint);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TokenShingleSimilarityFromFingerprint_returns_one_when_both_sides_have_no_tokens()
    {
        var oldFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(new[] { "   !!!" });
        var newFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(new[] { "\t---" });

        var result = BlockSimilarityScorer.TokenShingleSimilarityFromFingerprint(oldFingerprint, newFingerprint);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void SimHashSimilarityFromFingerprint_returns_one_when_both_sides_have_no_tokens()
    {
        var oldFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(new[] { "   !!!" });
        var newFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(new[] { "\t---" });

        var result = BlockSimilarityScorer.SimHashSimilarityFromFingerprint(oldFingerprint, newFingerprint);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void SimHashSimilarityFromFingerprint_returns_zero_when_only_one_side_has_tokens()
    {
        var oldFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(new[] { "foo bar baz" });
        var newFingerprint = BlockSimilarityScorer.ComputeFileFingerprint(new[] { "   !!!" });

        var result = BlockSimilarityScorer.SimHashSimilarityFromFingerprint(oldFingerprint, newFingerprint);

        Assert.Equal(0.0, result);
    }
}
