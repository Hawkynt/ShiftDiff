using System.Text;
using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class MarkdownComparerTests {
  private static byte[] Bytes(string markdown) => Encoding.UTF8.GetBytes(markdown);

  [Fact]
  public void Compare_IdenticalHeadingSections_AllUnchanged() {
    var changes = MarkdownComparer.Compare(
        Bytes("# Foo\nHello\n\n## Bar\nWorld\n"),
        Bytes("# Foo\nHello\n\n## Bar\nWorld\n"));

    Assert.All(changes, c => Assert.Equal(MarkdownChangeType.Unchanged, c.ChangeType));
    Assert.All(changes, c => Assert.Null(c.BodyChanges));
    Assert.Equal(2, changes.Length);
  }

  [Fact]
  public void Compare_SectionContentChanged_MarksChanged() {
    var changes = MarkdownComparer.Compare(
        Bytes("# Foo\nHello\n"),
        Bytes("# Foo\nHi\n"));

    var change = Assert.Single(changes);
    Assert.Equal("# Foo", change.Path);
    Assert.Equal(MarkdownChangeType.Changed, change.ChangeType);
    Assert.Equal("Hello", change.OldValue);
    Assert.Equal("Hi", change.NewValue);
  }

  [Fact]
  public void Compare_SectionContentChanged_BodyChangesIsolateTheChangedLine() {
    var changes = MarkdownComparer.Compare(
        Bytes("# Foo\nline1\nline2\n"),
        Bytes("# Foo\nline1\nCHANGED\n"));

    var change = Assert.Single(changes);
    Assert.NotNull(change.BodyChanges);
    Assert.Contains(change.BodyChanges!, lc => lc.ChangeType == ChangeType.Edited && lc.OldLine == "line2" && lc.NewLine == "CHANGED");
  }

  [Fact]
  public void Compare_HeadingAdded_MarksAdded() {
    var changes = MarkdownComparer.Compare(
        Bytes("# Foo\nHello\n"),
        Bytes("# Foo\nHello\n\n# Baz\nNew\n"));

    var change = Assert.Single(changes, c => c.ChangeType == MarkdownChangeType.Added);
    Assert.Equal("# Baz", change.Path);
    Assert.Null(change.OldValue);
    Assert.Equal("New", change.NewValue);
    Assert.Null(change.BodyChanges);
  }

  [Fact]
  public void Compare_HeadingRemoved_MarksRemoved() {
    var changes = MarkdownComparer.Compare(
        Bytes("# Foo\nHello\n\n# Baz\nOld\n"),
        Bytes("# Foo\nHello\n"));

    var change = Assert.Single(changes, c => c.ChangeType == MarkdownChangeType.Removed);
    Assert.Equal("# Baz", change.Path);
    Assert.Equal("Old", change.OldValue);
    Assert.Null(change.NewValue);
    Assert.Null(change.BodyChanges);
  }

  [Fact]
  public void Compare_HeadingsReordered_ContentSame_StillAllUnchanged() {
    var changes = MarkdownComparer.Compare(
        Bytes("# Foo\nA\n\n# Bar\nB\n"),
        Bytes("# Bar\nB\n\n# Foo\nA\n"));

    Assert.All(changes, c => Assert.Equal(MarkdownChangeType.Unchanged, c.ChangeType));
    Assert.Equal(2, changes.Length);
  }

  [Fact]
  public void Compare_PreambleContentChanged_MarksChangedAtEmptyPath() {
    var changes = MarkdownComparer.Compare(
        Bytes("Intro text\n\n# Foo\nA\n"),
        Bytes("Different intro\n\n# Foo\nA\n"));

    var change = Assert.Single(changes, c => c.Path == "");
    Assert.Equal(MarkdownChangeType.Changed, change.ChangeType);
    Assert.Equal("Intro text", change.OldValue);
    Assert.Equal("Different intro", change.NewValue);
  }

  [Fact]
  public void Compare_NoPreambleText_NoEmptyPathEntry() {
    var changes = MarkdownComparer.Compare(
        Bytes("# Foo\nA\n"),
        Bytes("# Foo\nA\n"));

    Assert.DoesNotContain(changes, c => c.Path == "");
  }

  [Fact]
  public void Compare_NestedHeadingsSameTextDifferentParents_EditDetectedSeparately() {
    var changes = MarkdownComparer.Compare(
        Bytes("# A\n## X\nfirst\n\n# B\n## X\nsecond\n"),
        Bytes("# A\n## X\nfirst-CHANGED\n\n# B\n## X\nsecond\n"));

    var change = Assert.Single(changes, c => c.ChangeType == MarkdownChangeType.Changed);
    Assert.Equal("# A > ## X", change.Path);
    Assert.Equal("first", change.OldValue);
    Assert.Equal("first-CHANGED", change.NewValue);

    var untouched = Assert.Single(changes, c => c.Path == "# B > ## X");
    Assert.Equal(MarkdownChangeType.Unchanged, untouched.ChangeType);
  }

  [Fact]
  public void Compare_SkippedIntermediateHeadingLevel_DoesNotCrashAndKeysByPresentAncestors() {
    var changes = MarkdownComparer.Compare(
        Bytes("# A\n### B\nfirst\n"),
        Bytes("# A\n### B\nfirst-CHANGED\n"));

    var change = Assert.Single(changes, c => c.ChangeType == MarkdownChangeType.Changed);
    Assert.Equal("# A > ### B", change.Path);
    Assert.Equal("first", change.OldValue);
    Assert.Equal("first-CHANGED", change.NewValue);
  }

  [Fact]
  public void Compare_HeadingLikeLineInsideCodeFence_StaysPartOfEnclosingSection() {
    var changes = MarkdownComparer.Compare(
        Bytes("# Foo\n```\n# not a heading\ncode line\n```\n"),
        Bytes("# Foo\n```\n# not a heading\ncode line changed\n```\n"));

    var change = Assert.Single(changes);
    Assert.Equal("# Foo", change.Path);
    Assert.Equal(MarkdownChangeType.Changed, change.ChangeType);
    Assert.Contains("# not a heading", change.OldValue);
    Assert.Contains("code line", change.OldValue);
  }

  [Fact]
  public void Compare_SetextLevel1Heading_KeyedSameAsEquivalentAtxH1() {
    var changes = MarkdownComparer.Compare(
        Bytes("Foo\n===\nHello\n"),
        Bytes("Foo\n===\nHi\n"));

    var change = Assert.Single(changes);
    Assert.Equal("# Foo", change.Path);
    Assert.Equal(MarkdownChangeType.Changed, change.ChangeType);
    Assert.Equal("Hello", change.OldValue);
    Assert.Equal("Hi", change.NewValue);
  }

  [Fact]
  public void Compare_SetextLevel2Heading_KeyedSameAsEquivalentAtxH2() {
    var changes = MarkdownComparer.Compare(
        Bytes("# A\nFoo\n---\nHello\n"),
        Bytes("# A\nFoo\n---\nHi\n"));

    var change = Assert.Single(changes, c => c.ChangeType == MarkdownChangeType.Changed);
    Assert.Equal("# A > ## Foo", change.Path);
    Assert.Equal("Hello", change.OldValue);
    Assert.Equal("Hi", change.NewValue);
  }

  [Fact]
  public void Compare_SetextHeadingStyleChangedToAtx_ContentSame_StillUnchanged() {
    var changes = MarkdownComparer.Compare(
        Bytes("Foo\n===\nHello\n"),
        Bytes("# Foo\nHello\n"));

    var change = Assert.Single(changes);
    Assert.Equal("# Foo", change.Path);
    Assert.Equal(MarkdownChangeType.Unchanged, change.ChangeType);
  }

  [Fact]
  public void Compare_BareDashLineAfterBlankLine_TreatedAsContentNotHeading() {
    var changes = MarkdownComparer.Compare(
        Bytes("# Foo\nHello\n\n---\nWorld\n"),
        Bytes("# Foo\nHello\n\n---\nWorld changed\n"));

    var change = Assert.Single(changes);
    Assert.Equal("# Foo", change.Path);
    Assert.Equal(MarkdownChangeType.Changed, change.ChangeType);
    Assert.Contains("---", change.OldValue!);
    Assert.Contains("World", change.OldValue!);
  }
}
