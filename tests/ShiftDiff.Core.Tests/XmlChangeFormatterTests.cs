using Xunit;

namespace ShiftDiff.Core.Tests;

public class XmlChangeFormatterTests {
  [Fact]
  public void Format_ChangedValue_PrintsOldArrowNew() {
    var lines = XmlChangeFormatter.Format(new[]
    {
            new XmlChange("a/key", XmlChangeType.Changed, "1", "2"),
        });

    Assert.Equal(new[] { "a/key: Changed 1 -> 2" }, lines);
  }

  [Fact]
  public void Format_AddedValue_PrintsArrowNewWithNoOldValue() {
    var lines = XmlChangeFormatter.Format(new[]
    {
            new XmlChange("a/key", XmlChangeType.Added, null, "2"),
        });

    Assert.Equal(new[] { "a/key: Added -> 2" }, lines);
  }

  [Fact]
  public void Format_RemovedValue_PrintsOldArrowWithNoNewValue() {
    var lines = XmlChangeFormatter.Format(new[]
    {
            new XmlChange("a/key", XmlChangeType.Removed, "1", null),
        });

    Assert.Equal(new[] { "a/key: Removed 1 ->" }, lines);
  }

  [Fact]
  public void Format_UnchangedValue_ProducesNoLine() {
    var lines = XmlChangeFormatter.Format(new[]
    {
            new XmlChange("a/key", XmlChangeType.Unchanged, "1", "1"),
        });

    Assert.Empty(lines);
  }

  [Fact]
  public void Format_MixOfChangeTypes_SkipsOnlyUnchanged() {
    var lines = XmlChangeFormatter.Format(new[]
    {
            new XmlChange("a/key", XmlChangeType.Unchanged, "1", "1"),
            new XmlChange("a/other", XmlChangeType.Added, null, "2"),
        });

    Assert.Equal(new[] { "a/other: Added -> 2" }, lines);
  }

  [Fact]
  public void Format_NullPath_UsesRootPlaceholder() {
    var lines = XmlChangeFormatter.Format(new[]
    {
            new XmlChange(null, XmlChangeType.Changed, "1", "2"),
        });

    Assert.Equal(new[] { "(root): Changed 1 -> 2" }, lines);
  }
}
