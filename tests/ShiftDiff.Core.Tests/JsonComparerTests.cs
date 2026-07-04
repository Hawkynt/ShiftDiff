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
}
