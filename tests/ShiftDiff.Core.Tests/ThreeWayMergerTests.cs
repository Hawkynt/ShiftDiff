using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class ThreeWayMergerTests
{
    [Fact]
    public void Merge_AllUnchanged_ReturnsBaseLinesNoConflicts()
    {
        var baseLines = new[] { "a", "b", "c" };
        var changes = ThreeWayComparer.Compare(baseLines, baseLines, baseLines);

        var result = ThreeWayMerger.Merge(changes);

        Assert.Equal(baseLines, result.Lines);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Merge_LocalOnlyEdit_TakesLocalValue()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "B", "c" };
        var remoteLines = new[] { "a", "b", "c" };
        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var result = ThreeWayMerger.Merge(changes);

        Assert.Equal(localLines, result.Lines);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Merge_BothSidesEditDifferently_OmitsLineAndReportsConflict()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "B-local", "c" };
        var remoteLines = new[] { "a", "B-remote", "c" };
        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var result = ThreeWayMerger.Merge(changes);

        Assert.Equal(new[] { "a", "c" }, result.Lines);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal(ChangeType.Conflict, conflict.ChangeType);
        Assert.Equal(1, conflict.BaseIndex);
    }

    [Fact]
    public void Merge_LocalOnlyDeletion_DropsLine()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "c" };
        var remoteLines = new[] { "a", "b", "c" };
        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var result = ThreeWayMerger.Merge(changes);

        Assert.Equal(new[] { "a", "c" }, result.Lines);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Merge_LocalOnlyInsertion_IncludesLineAtCorrectPosition()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "X", "b", "c" };
        var remoteLines = new[] { "a", "b", "c" };
        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var result = ThreeWayMerger.Merge(changes);

        Assert.Equal(localLines, result.Lines);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Merge_BothSidesInsertSameLineAtSameSpot_IncludesLineOnce()
    {
        var baseLines = new[] { "a", "b" };
        var localLines = new[] { "a", "X", "b" };
        var remoteLines = new[] { "a", "X", "b" };
        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var result = ThreeWayMerger.Merge(changes);

        Assert.Equal(localLines, result.Lines);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void MergeWithResolutions_NoResolutionsGiven_BehavesLikeMerge()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "B-local", "c" };
        var remoteLines = new[] { "a", "B-remote", "c" };
        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var result = ThreeWayMerger.MergeWithResolutions(changes,
            new Dictionary<int, ConflictResolutionChoice>());

        Assert.Equal(new[] { "a", "c" }, result.Lines);
        Assert.Single(result.Conflicts);
    }

    [Fact]
    public void MergeWithResolutions_ConflictResolvedUseLocal_IncludesLocalLineNoConflict()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "B-local", "c" };
        var remoteLines = new[] { "a", "B-remote", "c" };
        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);
        var resolutions = new Dictionary<int, ConflictResolutionChoice>
        {
            [1] = new ConflictResolutionChoice(ConflictResolution.UseLocal),
        };

        var result = ThreeWayMerger.MergeWithResolutions(changes, resolutions);

        Assert.Equal(new[] { "a", "B-local", "c" }, result.Lines);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void MergeWithResolutions_ConflictResolvedUseRemote_IncludesRemoteLineNoConflict()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "B-local", "c" };
        var remoteLines = new[] { "a", "B-remote", "c" };
        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);
        var resolutions = new Dictionary<int, ConflictResolutionChoice>
        {
            [1] = new ConflictResolutionChoice(ConflictResolution.UseRemote),
        };

        var result = ThreeWayMerger.MergeWithResolutions(changes, resolutions);

        Assert.Equal(new[] { "a", "B-remote", "c" }, result.Lines);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void MergeWithResolutions_ConflictResolvedUseCustom_IncludesCustomLineNoConflict()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "B-local", "c" };
        var remoteLines = new[] { "a", "B-remote", "c" };
        var changes = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);
        var resolutions = new Dictionary<int, ConflictResolutionChoice>
        {
            [1] = new ConflictResolutionChoice(ConflictResolution.UseCustom, "B-merged-by-hand"),
        };

        var result = ThreeWayMerger.MergeWithResolutions(changes, resolutions);

        Assert.Equal(new[] { "a", "B-merged-by-hand", "c" }, result.Lines);
        Assert.Empty(result.Conflicts);
    }
}
