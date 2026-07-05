using System.Linq;
using System.Text;
using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class IniComparerTests
{
    private static byte[] Bytes(string ini) => Encoding.UTF8.GetBytes(ini);

    [Fact]
    public void Compare_IdenticalSectionedKeys_AllUnchanged()
    {
        var changes = IniComparer.Compare(
            Bytes("[a]\nkey=1\n"),
            Bytes("[a]\nkey=1\n"));

        var change = Assert.Single(changes);
        Assert.Equal("a.key", change.Path);
        Assert.Equal(IniChangeType.Unchanged, change.ChangeType);
    }

    [Fact]
    public void Compare_ValueChanged_MarksChanged()
    {
        var changes = IniComparer.Compare(
            Bytes("[a]\nkey=1\n"),
            Bytes("[a]\nkey=2\n"));

        var change = Assert.Single(changes);
        Assert.Equal("a.key", change.Path);
        Assert.Equal(IniChangeType.Changed, change.ChangeType);
        Assert.Equal("1", change.OldValue);
        Assert.Equal("2", change.NewValue);
    }

    [Fact]
    public void Compare_KeyAdded_MarksAdded()
    {
        var changes = IniComparer.Compare(
            Bytes("[a]\nkey=1\n"),
            Bytes("[a]\nkey=1\nother=2\n"));

        var change = Assert.Single(changes, c => c.ChangeType == IniChangeType.Added);
        Assert.Equal("a.other", change.Path);
        Assert.Null(change.OldValue);
        Assert.Equal("2", change.NewValue);
    }

    [Fact]
    public void Compare_KeyRemoved_MarksRemoved()
    {
        var changes = IniComparer.Compare(
            Bytes("[a]\nkey=1\nother=2\n"),
            Bytes("[a]\nkey=1\n"));

        var change = Assert.Single(changes, c => c.ChangeType == IniChangeType.Removed);
        Assert.Equal("a.other", change.Path);
        Assert.Equal("2", change.OldValue);
        Assert.Null(change.NewValue);
    }

    [Fact]
    public void Compare_SectionsReordered_ValuesSame_StillAllUnchanged()
    {
        var changes = IniComparer.Compare(
            Bytes("[a]\nx=1\n[b]\ny=2\n"),
            Bytes("[b]\ny=2\n[a]\nx=1\n"));

        Assert.All(changes, c => Assert.Equal(IniChangeType.Unchanged, c.ChangeType));
        Assert.Equal(2, changes.Length);
    }

    [Fact]
    public void Compare_TopLevelKeyBeforeAnySection_PathIsKeyNameOnly()
    {
        var changes = IniComparer.Compare(
            Bytes("key=1\n"),
            Bytes("key=2\n"));

        var change = Assert.Single(changes);
        Assert.Equal("key", change.Path);
        Assert.Equal(IniChangeType.Changed, change.ChangeType);
    }

    [Fact]
    public void Compare_SemicolonAndHashComments_Ignored()
    {
        var changes = IniComparer.Compare(
            Bytes("; comment\n[a]\n# another comment\nkey=1\n"),
            Bytes("[a]\nkey=1\n"));

        var change = Assert.Single(changes);
        Assert.Equal("a.key", change.Path);
        Assert.Equal(IniChangeType.Unchanged, change.ChangeType);
    }

    [Fact]
    public void Compare_BlankLines_Ignored()
    {
        var changes = IniComparer.Compare(
            Bytes("[a]\n\nkey=1\n\n"),
            Bytes("[a]\nkey=1\n"));

        var change = Assert.Single(changes);
        Assert.Equal("a.key", change.Path);
        Assert.Equal(IniChangeType.Unchanged, change.ChangeType);
    }

    [Fact]
    public void Compare_DottedKeyAndSectionCollide_ProduceDistinctPaths()
    {
        var changes = IniComparer.Compare(
            Bytes(""),
            Bytes("[a]\nb.c=1\n[a.b]\nc=2\n"));

        Assert.Equal(2, changes.Length);
        Assert.All(changes, c => Assert.Equal(IniChangeType.Added, c.ChangeType));
        var paths = changes.Select(c => c.Path).ToArray();
        Assert.Contains(paths, p => p.EndsWith("b\\.c", StringComparison.Ordinal));
        Assert.Contains(paths, p => p.EndsWith("b.c", StringComparison.Ordinal) && !p.EndsWith("b\\.c", StringComparison.Ordinal));
        Assert.Equal(2, paths.Distinct().Count());
    }

    [Fact]
    public void Compare_BackslashInSectionCollidesWithDottedGlobalKey_ProduceDistinctPaths()
    {
        // Global key "a.b" escapes its dot to "a\.b". A section literally named
        // "a\" (raw trailing backslash) followed by key "b" composes to the same
        // "a\" + "." + "b" = "a\.b" unless the backslash itself is escaped first.
        var changes = IniComparer.Compare(
            Bytes(""),
            Bytes("a.b=1\n[a\\]\nb=2\n"));

        Assert.Equal(2, changes.Length);
        Assert.All(changes, c => Assert.Equal(IniChangeType.Added, c.ChangeType));
        var paths = changes.Select(c => c.Path).ToArray();
        Assert.Equal(2, paths.Distinct().Count());
    }

    [Fact]
    public void Compare_MalformedLineWithNoEqualsSign_IsSilentlyIgnored()
    {
        var changes = IniComparer.Compare(
            Bytes("[a]\nkey=1\njustnoise\n"),
            Bytes("[a]\nkey=2\njustnoise\n"));

        var change = Assert.Single(changes);
        Assert.Equal("a.key", change.Path);
        Assert.Equal(IniChangeType.Changed, change.ChangeType);
        Assert.Equal("1", change.OldValue);
        Assert.Equal("2", change.NewValue);
    }
}
