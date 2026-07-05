using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class SplitMergeDetectorTests
{
    [Fact]
    public void Detect_reclassifies_old_adjacent_new_distant_moved_pair_as_split()
    {
        var matches = new[]
        {
            new BlockMatch(1, 2, 10, 11, ChangeType.Moved, 0.9, Confidence.Certain),
            new BlockMatch(3, 4, 50, 51, ChangeType.Moved, 0.9, Confidence.Certain),
        };

        var result = SplitMergeDetector.Detect(matches);

        Assert.Equal(ChangeType.Split, result[0].MatchType);
        Assert.Equal(ChangeType.Split, result[1].MatchType);
    }

    [Fact]
    public void Detect_leaves_matches_unchanged_when_old_side_is_not_adjacent()
    {
        var matches = new[]
        {
            new BlockMatch(1, 2, 10, 11, ChangeType.Moved, 0.9, Confidence.Certain),
            new BlockMatch(5, 6, 50, 51, ChangeType.Moved, 0.9, Confidence.Certain),
        };

        var result = SplitMergeDetector.Detect(matches);

        Assert.Equal(ChangeType.Moved, result[0].MatchType);
        Assert.Equal(ChangeType.Moved, result[1].MatchType);
    }

    [Fact]
    public void Detect_does_not_split_when_either_side_is_uncertain()
    {
        var matches = new[]
        {
            new BlockMatch(1, 2, 10, 11, ChangeType.Uncertain, 0.2, Confidence.Weak),
            new BlockMatch(3, 4, 50, 51, ChangeType.Moved, 0.9, Confidence.Certain),
        };

        var result = SplitMergeDetector.Detect(matches);

        Assert.Equal(ChangeType.Uncertain, result[0].MatchType);
        Assert.Equal(ChangeType.Moved, result[1].MatchType);
    }

    [Fact]
    public void Detect_reclassifies_new_adjacent_old_distant_moved_pair_as_merged()
    {
        var matches = new[]
        {
            new BlockMatch(50, 51, 3, 4, ChangeType.Moved, 0.9, Confidence.Certain),
            new BlockMatch(10, 11, 1, 2, ChangeType.Moved, 0.9, Confidence.Certain),
        };

        var result = SplitMergeDetector.Detect(matches);

        Assert.Equal(ChangeType.Merged, result[0].MatchType);
        Assert.Equal(ChangeType.Merged, result[1].MatchType);
    }

    [Fact]
    public void Detect_leaves_matches_unchanged_when_new_side_is_not_adjacent()
    {
        var matches = new[]
        {
            new BlockMatch(50, 51, 3, 4, ChangeType.Moved, 0.9, Confidence.Certain),
            new BlockMatch(10, 11, 1, 1, ChangeType.Moved, 0.9, Confidence.Certain),
        };

        var result = SplitMergeDetector.Detect(matches);

        Assert.Equal(ChangeType.Moved, result[0].MatchType);
        Assert.Equal(ChangeType.Moved, result[1].MatchType);
    }

    [Fact]
    public void Detect_leaves_an_unrelated_third_match_unchanged_when_only_one_pair_qualifies()
    {
        var matches = new[]
        {
            new BlockMatch(1, 2, 10, 11, ChangeType.Moved, 0.9, Confidence.Certain),
            new BlockMatch(3, 4, 50, 51, ChangeType.Moved, 0.9, Confidence.Certain),
            new BlockMatch(20, 21, 90, 91, ChangeType.Moved, 0.9, Confidence.Certain),
        };

        var result = SplitMergeDetector.Detect(matches);

        Assert.Equal(ChangeType.Split, result[0].MatchType);
        Assert.Equal(ChangeType.Split, result[1].MatchType);
        Assert.Equal(ChangeType.Moved, result[2].MatchType);
    }

    [Fact]
    public void Detect_reclassifies_all_three_as_split_when_old_side_is_a_contiguous_chain()
    {
        var matches = new[]
        {
            new BlockMatch(1, 2, 10, 11, ChangeType.Moved, 0.9, Confidence.Certain),
            new BlockMatch(3, 4, 50, 51, ChangeType.Moved, 0.9, Confidence.Certain),
            new BlockMatch(5, 6, 90, 91, ChangeType.Moved, 0.9, Confidence.Certain),
        };

        var result = SplitMergeDetector.Detect(matches);

        Assert.Equal(ChangeType.Split, result[0].MatchType);
        Assert.Equal(ChangeType.Split, result[1].MatchType);
        Assert.Equal(ChangeType.Split, result[2].MatchType);
    }
}
