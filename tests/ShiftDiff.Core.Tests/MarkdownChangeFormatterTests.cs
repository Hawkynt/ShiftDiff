using Xunit;

namespace ShiftDiff.Core.Tests;

public class MarkdownChangeFormatterTests {
  [Fact]
  public void Format_ChangedValue_PrintsOldArrowNew() {
    var lines = MarkdownChangeFormatter.Format(new[]
    {
            new MarkdownChange("# A", MarkdownChangeType.Changed, "old body", "new body"),
        });

    Assert.Equal(new[] { "# A: Changed old body -> new body" }, lines);
  }

  [Fact]
  public void Format_AddedValue_PrintsArrowNewWithNoOldValue() {
    var lines = MarkdownChangeFormatter.Format(new[]
    {
            new MarkdownChange("# New", MarkdownChangeType.Added, null, "content"),
        });

    Assert.Equal(new[] { "# New: Added -> content" }, lines);
  }

  [Fact]
  public void Format_RemovedValue_PrintsOldArrowWithNoNewValue() {
    var lines = MarkdownChangeFormatter.Format(new[]
    {
            new MarkdownChange("# Old", MarkdownChangeType.Removed, "content", null),
        });

    Assert.Equal(new[] { "# Old: Removed content ->" }, lines);
  }

  [Fact]
  public void Format_UnchangedValue_ProducesNoLine() {
    var lines = MarkdownChangeFormatter.Format(new[]
    {
            new MarkdownChange("# A", MarkdownChangeType.Unchanged, "same", "same"),
        });

    Assert.Empty(lines);
  }

  [Fact]
  public void Format_MovedValue_PrintsMovedFromWithNoOldNewValues() {
    var lines = MarkdownChangeFormatter.Format(new[]
    {
            new MarkdownChange("# New", MarkdownChangeType.Moved, MovedFrom: "# Old"),
        });

    Assert.Equal(new[] { "# New: Moved (from # Old)" }, lines);
  }

  [Fact]
  public void Format_MovedEditedValue_PrintsMovedFromAndOldArrowNew() {
    var lines = MarkdownChangeFormatter.Format(new[]
    {
            new MarkdownChange("# New", MarkdownChangeType.MovedEdited, "old body", "new body", MovedFrom: "# Old"),
        });

    Assert.Equal(new[] { "# New: MovedEdited (from # Old) old body -> new body" }, lines);
  }
}
