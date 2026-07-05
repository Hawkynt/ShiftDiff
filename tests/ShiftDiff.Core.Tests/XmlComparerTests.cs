using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class XmlComparerTests
{
    [Fact]
    public void Compare_IdenticalRootAttributes_AllUnchanged()
    {
        var changes = XmlComparer.Compare(
            """<a b="1" c="2"/>"""u8.ToArray(),
            """<a b="1" c="2"/>"""u8.ToArray());

        Assert.All(changes, c => Assert.Equal(XmlChangeType.Unchanged, c.ChangeType));
        Assert.Equal(2, changes.Length);
    }

    [Fact]
    public void Compare_AttributesReordered_ValuesSame_StillAllUnchanged()
    {
        var changes = XmlComparer.Compare(
            """<a b="1" c="2"/>"""u8.ToArray(),
            """<a c="2" b="1"/>"""u8.ToArray());

        Assert.All(changes, c => Assert.Equal(XmlChangeType.Unchanged, c.ChangeType));
        Assert.Equal(2, changes.Length);
    }

    [Fact]
    public void Compare_AttributeValueChanged_MarksChanged()
    {
        var changes = XmlComparer.Compare(
            """<a b="1"/>"""u8.ToArray(),
            """<a b="2"/>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("@b", change.Path);
        Assert.Equal(XmlChangeType.Changed, change.ChangeType);
        Assert.Equal("1", change.OldValue);
        Assert.Equal("2", change.NewValue);
    }

    [Fact]
    public void Compare_AttributeAdded_MarksAdded()
    {
        var changes = XmlComparer.Compare(
            """<a b="1"/>"""u8.ToArray(),
            """<a b="1" c="2"/>"""u8.ToArray());

        var change = Assert.Single(changes, c => c.ChangeType == XmlChangeType.Added);
        Assert.Equal("@c", change.Path);
        Assert.Null(change.OldValue);
        Assert.Equal("2", change.NewValue);
    }

    [Fact]
    public void Compare_AttributeRemoved_MarksRemoved()
    {
        var changes = XmlComparer.Compare(
            """<a b="1" c="2"/>"""u8.ToArray(),
            """<a b="1"/>"""u8.ToArray());

        var change = Assert.Single(changes, c => c.ChangeType == XmlChangeType.Removed);
        Assert.Equal("@c", change.Path);
        Assert.Equal("2", change.OldValue);
        Assert.Null(change.NewValue);
    }
}
