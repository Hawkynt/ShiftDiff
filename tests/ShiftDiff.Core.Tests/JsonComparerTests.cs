using System.Linq;
using System.Text;
using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class JsonComparerTests
{
    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void Compare_IdenticalFlatObjects_AllUnchanged()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{"a": 1, "b": "x"}"""),
            Bytes("""{"a": 1, "b": "x"}"""));

        Assert.All(changes, c => Assert.Equal(JsonChangeType.Unchanged, c.ChangeType));
        Assert.Equal(2, changes.Length);
    }

    [Fact]
    public void Compare_KeysReordered_ValuesSame_StillAllUnchanged()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{"a": 1, "b": "x"}"""),
            Bytes("""{"b": "x", "a": 1}"""));

        Assert.All(changes, c => Assert.Equal(JsonChangeType.Unchanged, c.ChangeType));
        Assert.Equal(2, changes.Length);
    }

    [Fact]
    public void Compare_ValueChanged_MarksChanged()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{"a": 1}"""),
            Bytes("""{"a": 2}"""));

        var change = Assert.Single(changes);
        Assert.Equal("a", change.Path);
        Assert.Equal(JsonChangeType.Changed, change.ChangeType);
        Assert.Equal("1", change.OldValue);
        Assert.Equal("2", change.NewValue);
    }

    [Fact]
    public void Compare_KeyAdded_MarksAdded()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{"a": 1}"""),
            Bytes("""{"a": 1, "b": 2}"""));

        var change = Assert.Single(changes, c => c.ChangeType == JsonChangeType.Added);
        Assert.Equal("b", change.Path);
        Assert.Null(change.OldValue);
        Assert.Equal("2", change.NewValue);
    }

    [Fact]
    public void Compare_KeyRemoved_MarksRemoved()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{"a": 1, "b": 2}"""),
            Bytes("""{"a": 1}"""));

        var change = Assert.Single(changes, c => c.ChangeType == JsonChangeType.Removed);
        Assert.Equal("b", change.Path);
        Assert.Equal("2", change.OldValue);
        Assert.Null(change.NewValue);
    }

    [Fact]
    public void Compare_NestedObject_KeysReordered_StillUnchanged()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{"a": {"x": 1, "y": 2}}"""),
            Bytes("""{"a": {"y": 2, "x": 1}}"""));

        Assert.All(changes, c => Assert.Equal(JsonChangeType.Unchanged, c.ChangeType));
        Assert.Equal(["a.x", "a.y"], changes.Select(c => c.Path).OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void Compare_NestedObject_ValueChanged_MarksChangedAtNestedPath()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{"a": {"x": 1}}"""),
            Bytes("""{"a": {"x": 2}}"""));

        var change = Assert.Single(changes);
        Assert.Equal("a.x", change.Path);
        Assert.Equal(JsonChangeType.Changed, change.ChangeType);
        Assert.Equal("1", change.OldValue);
        Assert.Equal("2", change.NewValue);
    }

    [Fact]
    public void Compare_NestedObjectAdded_MarksAddedAtParentPathOnly()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{}"""),
            Bytes("""{"a": {"x": 1, "y": 2}}"""));

        var change = Assert.Single(changes);
        Assert.Equal("a", change.Path);
        Assert.Equal(JsonChangeType.Added, change.ChangeType);
        Assert.Null(change.OldValue);
    }

    [Fact]
    public void Compare_NestedObjectRemoved_MarksRemovedAtParentPathOnly()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{"a": {"x": 1, "y": 2}}"""),
            Bytes("""{}"""));

        var change = Assert.Single(changes);
        Assert.Equal("a", change.Path);
        Assert.Equal(JsonChangeType.Removed, change.ChangeType);
        Assert.Null(change.NewValue);
    }

    [Fact]
    public void Compare_TypeMismatch_ObjectVsScalar_MarksChangedAtParentPath()
    {
        var changes = JsonComparer.Compare(
            Bytes("""{"a": {"x": 1}}"""),
            Bytes("""{"a": 1}"""));

        var change = Assert.Single(changes);
        Assert.Equal("a", change.Path);
        Assert.Equal(JsonChangeType.Changed, change.ChangeType);
        Assert.Equal("""{"x":1}""", change.OldValue!.Replace(" ", ""));
        Assert.Equal("1", change.NewValue);
    }
}
