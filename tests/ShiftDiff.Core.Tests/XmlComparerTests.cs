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

    [Fact]
    public void Compare_IdenticalChildElement_AllUnchanged()
    {
        var changes = XmlComparer.Compare(
            """<a><b x="1"/></a>"""u8.ToArray(),
            """<a><b x="1"/></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("b/@x", change.Path);
        Assert.Equal(XmlChangeType.Unchanged, change.ChangeType);
    }

    [Fact]
    public void Compare_ChildElementAttributeChanged_MarksChangedAtNestedPath()
    {
        var changes = XmlComparer.Compare(
            """<a><b x="1"/></a>"""u8.ToArray(),
            """<a><b x="2"/></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("b/@x", change.Path);
        Assert.Equal(XmlChangeType.Changed, change.ChangeType);
        Assert.Equal("1", change.OldValue);
        Assert.Equal("2", change.NewValue);
    }

    [Fact]
    public void Compare_ChildElementAdded_MarksAddedAtChildPathOnly()
    {
        var changes = XmlComparer.Compare(
            """<a></a>"""u8.ToArray(),
            """<a><b x="1"/></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("b", change.Path);
        Assert.Equal(XmlChangeType.Added, change.ChangeType);
        Assert.Null(change.OldValue);
        Assert.NotNull(change.NewValue);
    }

    [Fact]
    public void Compare_ChildElementRemoved_MarksRemovedAtChildPathOnly()
    {
        var changes = XmlComparer.Compare(
            """<a><b x="1"/></a>"""u8.ToArray(),
            """<a></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("b", change.Path);
        Assert.Equal(XmlChangeType.Removed, change.ChangeType);
        Assert.Null(change.NewValue);
        Assert.NotNull(change.OldValue);
    }

    [Fact]
    public void Compare_GrandchildElementAttributeChanged_MarksChangedAtDeepPath()
    {
        var changes = XmlComparer.Compare(
            """<a><b><c x="1"/></b></a>"""u8.ToArray(),
            """<a><b><c x="2"/></b></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("b/c/@x", change.Path);
        Assert.Equal(XmlChangeType.Changed, change.ChangeType);
    }

    [Fact]
    public void Compare_RepeatedSiblingName_SkippedNotThrown()
    {
        var changes = XmlComparer.Compare(
            """<a><item n="1"/><item n="2"/></a>"""u8.ToArray(),
            """<a><item n="1"/><item n="9"/></a>"""u8.ToArray());

        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_LeafElementTextChanged_MarksChangedAtChildPath()
    {
        var changes = XmlComparer.Compare(
            """<a><name>old</name></a>"""u8.ToArray(),
            """<a><name>new</name></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("name", change.Path);
        Assert.Equal(XmlChangeType.Changed, change.ChangeType);
        Assert.Equal("old", change.OldValue);
        Assert.Equal("new", change.NewValue);
    }

    [Fact]
    public void Compare_LeafElementTextSame_MarksUnchanged()
    {
        var changes = XmlComparer.Compare(
            """<a><name>same</name></a>"""u8.ToArray(),
            """<a><name>same</name></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("name", change.Path);
        Assert.Equal(XmlChangeType.Unchanged, change.ChangeType);
    }

    [Fact]
    public void Compare_LeafElementTextAdded_MarksAdded()
    {
        var changes = XmlComparer.Compare(
            """<a><name></name></a>"""u8.ToArray(),
            """<a><name>value</name></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("name", change.Path);
        Assert.Equal(XmlChangeType.Added, change.ChangeType);
        Assert.Null(change.OldValue);
        Assert.Equal("value", change.NewValue);
    }

    [Fact]
    public void Compare_LeafElementTextRemoved_MarksRemoved()
    {
        var changes = XmlComparer.Compare(
            """<a><name>value</name></a>"""u8.ToArray(),
            """<a><name></name></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("name", change.Path);
        Assert.Equal(XmlChangeType.Removed, change.ChangeType);
        Assert.Equal("value", change.OldValue);
        Assert.Null(change.NewValue);
    }

    [Fact]
    public void Compare_RootLeafTextChanged_MarksChangedAtNullPath()
    {
        var changes = XmlComparer.Compare(
            """<a>hello</a>"""u8.ToArray(),
            """<a>world</a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Null(change.Path);
        Assert.Equal(XmlChangeType.Changed, change.ChangeType);
        Assert.Equal("hello", change.OldValue);
        Assert.Equal("world", change.NewValue);
    }

    [Fact]
    public void Compare_WhitespaceOnlyTextAroundChildElement_IgnoredNotCompared()
    {
        var changes = XmlComparer.Compare(
            """<a>  <b x="1"/>  </a>"""u8.ToArray(),
            """<a><b x="1"/></a>"""u8.ToArray());

        var change = Assert.Single(changes);
        Assert.Equal("b/@x", change.Path);
        Assert.Equal(XmlChangeType.Unchanged, change.ChangeType);
    }
}
