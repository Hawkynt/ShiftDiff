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

    [Fact]
    public void Diff_classifies_a_line_inserted_before_unchanged_tail_as_added_not_edited()
    {
        // Naive positional (index-by-index) comparison misclassifies this as a
        // cascade of Edited lines, since it never realigns after an insertion.
        var oldLines = new[] { "a", "b", "c" };
        var newLines = new[] { "a", "x", "b", "c" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(4, result.Length);
        Assert.Equal(ChangeType.Unchanged, result[0].ChangeType);
        Assert.Equal(ChangeType.Added, result[1].ChangeType);
        Assert.Equal(ChangeType.Unchanged, result[2].ChangeType);
        Assert.Equal(ChangeType.Unchanged, result[3].ChangeType);
    }

    [Fact]
    public void Diff_classifies_a_line_inserted_at_the_start_as_added_not_edited()
    {
        var oldLines = new[] { "a", "b", "c" };
        var newLines = new[] { "x", "a", "b", "c" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(4, result.Length);
        Assert.Equal(ChangeType.Added, result[0].ChangeType);
        Assert.Equal(ChangeType.Unchanged, result[1].ChangeType);
        Assert.Equal(ChangeType.Unchanged, result[2].ChangeType);
        Assert.Equal(ChangeType.Unchanged, result[3].ChangeType);
    }

    [Fact]
    public void Diff_classifies_a_line_removed_before_unchanged_tail_as_removed_not_edited()
    {
        var oldLines = new[] { "a", "x", "b", "c" };
        var newLines = new[] { "a", "b", "c" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(4, result.Length);
        Assert.Equal(ChangeType.Unchanged, result[0].ChangeType);
        Assert.Equal(ChangeType.Removed, result[1].ChangeType);
        Assert.Equal(ChangeType.Unchanged, result[2].ChangeType);
        Assert.Equal(ChangeType.Unchanged, result[3].ChangeType);
    }

    [Fact]
    public void Diff_pairs_disjoint_replacement_lines_as_edited_and_leaves_leftover_as_added()
    {
        var oldLines = new[] { "a", "b" };
        var newLines = new[] { "x", "y", "z" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(3, result.Length);
        Assert.Equal(ChangeType.Edited, result[0].ChangeType);
        Assert.Equal(ChangeType.Edited, result[1].ChangeType);
        Assert.Equal(ChangeType.Added, result[2].ChangeType);
    }

    [Fact]
    public void Diff_exposes_old_and_new_line_content_on_each_change()
    {
        var oldLines = new[] { "a", "b" };
        var newLines = new[] { "a", "B" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal("a", result[0].OldLine);
        Assert.Equal("a", result[0].NewLine);
        Assert.Equal("b", result[1].OldLine);
        Assert.Equal("B", result[1].NewLine);
    }

    [Fact]
    public void Diff_exposes_old_and_new_indices_for_unchanged_lines()
    {
        var oldLines = new[] { "a", "b" };
        var newLines = new[] { "a", "b" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(0, result[0].OldIndex);
        Assert.Equal(0, result[0].NewIndex);
        Assert.Equal(1, result[1].OldIndex);
        Assert.Equal(1, result[1].NewIndex);
    }

    [Fact]
    public void Diff_exposes_old_index_and_null_new_index_for_removed_lines()
    {
        var oldLines = new[] { "a", "b" };
        var newLines = new[] { "a" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(1, result[1].OldIndex);
        Assert.Null(result[1].NewIndex);
    }

    [Fact]
    public void Diff_exposes_new_index_and_null_old_index_for_added_lines()
    {
        var oldLines = new[] { "a" };
        var newLines = new[] { "a", "b" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(1, result[1].NewIndex);
        Assert.Null(result[1].OldIndex);
    }

    [Fact]
    public void Diff_exposes_both_indices_for_edited_pairs()
    {
        var oldLines = new[] { "a", "b" };
        var newLines = new[] { "a", "B" };

        var result = LineDiffer.Diff(oldLines, newLines);

        Assert.Equal(1, result[1].OldIndex);
        Assert.Equal(1, result[1].NewIndex);
    }
}
