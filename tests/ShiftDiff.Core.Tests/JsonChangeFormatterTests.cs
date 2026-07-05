using Xunit;

namespace ShiftDiff.Core.Tests;

public class JsonChangeFormatterTests
{
    [Fact]
    public void Format_ChangedValue_PrintsOldArrowNew()
    {
        var lines = JsonChangeFormatter.Format(new[]
        {
            new JsonChange("a.key", JsonChangeType.Changed, "1", "2"),
        });

        Assert.Equal(new[] { "a.key: Changed 1 -> 2" }, lines);
    }

    [Fact]
    public void Format_AddedValue_PrintsArrowNewWithNoOldValue()
    {
        var lines = JsonChangeFormatter.Format(new[]
        {
            new JsonChange("a.key", JsonChangeType.Added, null, "2"),
        });

        Assert.Equal(new[] { "a.key: Added -> 2" }, lines);
    }

    [Fact]
    public void Format_RemovedValue_PrintsOldArrowWithNoNewValue()
    {
        var lines = JsonChangeFormatter.Format(new[]
        {
            new JsonChange("a.key", JsonChangeType.Removed, "1", null),
        });

        Assert.Equal(new[] { "a.key: Removed 1 ->" }, lines);
    }

    [Fact]
    public void Format_UnchangedValue_ProducesNoLine()
    {
        var lines = JsonChangeFormatter.Format(new[]
        {
            new JsonChange("a.key", JsonChangeType.Unchanged, "1", "1"),
        });

        Assert.Empty(lines);
    }

    [Fact]
    public void Format_MixOfChangeTypes_SkipsOnlyUnchanged()
    {
        var lines = JsonChangeFormatter.Format(new[]
        {
            new JsonChange("a.key", JsonChangeType.Unchanged, "1", "1"),
            new JsonChange("a.other", JsonChangeType.Added, null, "2"),
        });

        Assert.Equal(new[] { "a.other: Added -> 2" }, lines);
    }

    [Fact]
    public void Format_NullPath_UsesRootPlaceholder()
    {
        var lines = JsonChangeFormatter.Format(new[]
        {
            new JsonChange(null, JsonChangeType.Changed, "1", "2"),
        });

        Assert.Equal(new[] { "(root): Changed 1 -> 2" }, lines);
    }
}
