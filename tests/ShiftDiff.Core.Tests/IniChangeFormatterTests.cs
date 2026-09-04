using Xunit;

namespace ShiftDiff.Core.Tests;

public class IniChangeFormatterTests {
  [Fact]
  public void Format_ChangedValue_PrintsOldArrowNew() {
    var lines = IniChangeFormatter.Format(new[]
    {
            new IniChange("a.key", IniChangeType.Changed, "1", "2"),
        });

    Assert.Equal(new[] { "a.key: Changed 1 -> 2" }, lines);
  }

  [Fact]
  public void Format_AddedValue_PrintsArrowNewWithNoOldValue() {
    var lines = IniChangeFormatter.Format(new[]
    {
            new IniChange("a.key", IniChangeType.Added, null, "2"),
        });

    Assert.Equal(new[] { "a.key: Added -> 2" }, lines);
  }

  [Fact]
  public void Format_RemovedValue_PrintsOldArrowWithNoNewValue() {
    var lines = IniChangeFormatter.Format(new[]
    {
            new IniChange("a.key", IniChangeType.Removed, "1", null),
        });

    Assert.Equal(new[] { "a.key: Removed 1 ->" }, lines);
  }

  [Fact]
  public void Format_UnchangedValue_ProducesNoLine() {
    var lines = IniChangeFormatter.Format(new[]
    {
            new IniChange("a.key", IniChangeType.Unchanged, "1", "1"),
        });

    Assert.Empty(lines);
  }

  [Fact]
  public void Format_MixOfChangeTypes_SkipsOnlyUnchanged() {
    var lines = IniChangeFormatter.Format(new[]
    {
            new IniChange("a.key", IniChangeType.Unchanged, "1", "1"),
            new IniChange("a.other", IniChangeType.Added, null, "2"),
        });

    Assert.Equal(new[] { "a.other: Added -> 2" }, lines);
  }
}
