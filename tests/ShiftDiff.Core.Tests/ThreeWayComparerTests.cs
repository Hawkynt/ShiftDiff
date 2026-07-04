using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class ThreeWayComparerTests
{
    [Fact]
    public void Compare_AllUnchanged_ReturnsAllUnchangedNoneSide()
    {
        var baseLines = new[] { "a", "b", "c" };

        var result = ThreeWayComparer.Compare(baseLines, baseLines, baseLines);

        Assert.All(result, c =>
        {
            Assert.Equal(ChangeType.Unchanged, c.ChangeType);
            Assert.Equal(ChangeSide.None, c.Side);
        });
    }

    [Fact]
    public void Compare_LocalOnlyEdit_MarksEditedSideLocal()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "B", "c" };
        var remoteLines = new[] { "a", "b", "c" };

        var result = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var change = Assert.Single(result, c => c.BaseIndex == 1);
        Assert.Equal(ChangeType.Edited, change.ChangeType);
        Assert.Equal(ChangeSide.Local, change.Side);
    }

    [Fact]
    public void Compare_RemoteOnlyEdit_MarksEditedSideRemote()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "b", "c" };
        var remoteLines = new[] { "a", "B", "c" };

        var result = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var change = Assert.Single(result, c => c.BaseIndex == 1);
        Assert.Equal(ChangeType.Edited, change.ChangeType);
        Assert.Equal(ChangeSide.Remote, change.Side);
    }

    [Fact]
    public void Compare_BothSidesEditIdentically_MarksEditedSideBothNotConflict()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "B", "c" };
        var remoteLines = new[] { "a", "B", "c" };

        var result = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var change = Assert.Single(result, c => c.BaseIndex == 1);
        Assert.Equal(ChangeType.Edited, change.ChangeType);
        Assert.Equal(ChangeSide.Both, change.Side);
    }

    [Fact]
    public void Compare_BothSidesEditDifferently_MarksConflictSideBoth()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "B-local", "c" };
        var remoteLines = new[] { "a", "B-remote", "c" };

        var result = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var change = Assert.Single(result, c => c.BaseIndex == 1);
        Assert.Equal(ChangeType.Conflict, change.ChangeType);
        Assert.Equal(ChangeSide.Both, change.Side);
    }

    [Fact]
    public void Compare_LocalOnlyDeletion_MarksRemovedSideLocal()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "c" };
        var remoteLines = new[] { "a", "b", "c" };

        var result = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var change = Assert.Single(result, c => c.BaseIndex == 1);
        Assert.Equal(ChangeType.Removed, change.ChangeType);
        Assert.Equal(ChangeSide.Local, change.Side);
        Assert.Null(change.LocalLine);
    }

    [Fact]
    public void Compare_RemoteOnlyDeletion_MarksRemovedSideRemote()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "b", "c" };
        var remoteLines = new[] { "a", "c" };

        var result = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var change = Assert.Single(result, c => c.BaseIndex == 1);
        Assert.Equal(ChangeType.Removed, change.ChangeType);
        Assert.Equal(ChangeSide.Remote, change.Side);
        Assert.Null(change.RemoteLine);
    }

    [Fact]
    public void Compare_BothSidesDeleteSameLine_MarksRemovedSideBoth()
    {
        var baseLines = new[] { "a", "b", "c" };
        var localLines = new[] { "a", "c" };
        var remoteLines = new[] { "a", "c" };

        var result = ThreeWayComparer.Compare(baseLines, localLines, remoteLines);

        var change = Assert.Single(result, c => c.BaseIndex == 1);
        Assert.Equal(ChangeType.Removed, change.ChangeType);
        Assert.Equal(ChangeSide.Both, change.Side);
    }
}
