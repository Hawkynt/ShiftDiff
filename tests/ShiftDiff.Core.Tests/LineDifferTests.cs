using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class LineDifferTests
{
    [Fact]
    public void Diff_classifies_identical_files_as_unchanged()
    {
        var oldLines = new[] { "line one", "line two", "line three" };
        var newLines = new[] { "line one", "line two", "line three" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.All(result, r => Assert.Equal(ChangeType.Unchanged, r.ChangeType));
    }

    [Fact]
    public void Diff_classifies_a_single_changed_line_as_edited()
    {
        var oldLines = new[] { "line one", "line two", "line three" };
        var newLines = new[] { "line one", "line TWO", "line three" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(ChangeType.Unchanged, result[0].ChangeType);
        Assert.Equal(ChangeType.Edited, result[1].ChangeType);
        Assert.Equal(ChangeType.Unchanged, result[2].ChangeType);
    }

    [Fact]
    public void Diff_classifies_a_trailing_new_line_as_added()
    {
        var oldLines = new[] { "line one" };
        var newLines = new[] { "line one", "line two" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(ChangeType.Unchanged, result[0].ChangeType);
        Assert.Equal(ChangeType.Added, result[1].ChangeType);
    }

    [Fact]
    public void Diff_classifies_a_removed_trailing_line_as_removed()
    {
        var oldLines = new[] { "line one", "line two" };
        var newLines = new[] { "line one" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(ChangeType.Unchanged, result[0].ChangeType);
        Assert.Equal(ChangeType.Removed, result[1].ChangeType);
    }
}
