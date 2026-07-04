using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class PatchApplierTests
{
    [Fact]
    public void AllContextHunk_NoActualChange_OutputEqualsInput()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Context, "b"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var result = PatchApplier.ApplyHunkExact(source, hunk);

        Assert.Equal(source, result);
    }

    [Fact]
    public void SingleHunk_ReplacingOneLine_RemovedAddedPair()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var result = PatchApplier.ApplyHunkExact(source, hunk);

        Assert.Equal(new[] { "a", "B", "c" }, result);
    }

    [Fact]
    public void HunkAtVeryStart_OldStartOne_AppliesFromBeginning()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 1, 1, 1),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Removed, "a"),
                new(UnifiedDiffLineKind.Added, "A"),
            });

        var result = PatchApplier.ApplyHunkExact(source, hunk);

        Assert.Equal(new[] { "A", "b", "c" }, result);
    }

    [Fact]
    public void HunkAtVeryEnd_CoversLastLine_AppliesThroughEnd()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(3, 1, 3, 1),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Removed, "c"),
                new(UnifiedDiffLineKind.Added, "C"),
            });

        var result = PatchApplier.ApplyHunkExact(source, hunk);

        Assert.Equal(new[] { "a", "b", "C" }, result);
    }

    [Fact]
    public void ContextMismatch_ThrowsPatchApplicationException()
    {
        var source = new[] { "a", "x", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        Assert.Throws<PatchApplicationException>(() => PatchApplier.ApplyHunkExact(source, hunk));
    }

    [Fact]
    public void MultiHunkFile_AppliesEachHunkInOrder()
    {
        var source = new[] { "a", "b", "c", "d", "e" };
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("a/f", "b/f"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 1, 1, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "a"),
                        new(UnifiedDiffLineKind.Added, "A"),
                    }),
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(5, 1, 5, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "e"),
                        new(UnifiedDiffLineKind.Added, "E"),
                    }),
            });

        var result = PatchApplier.ApplyFileExact(source, file);

        Assert.Equal(new[] { "A", "b", "c", "d", "E" }, result);
    }
}
