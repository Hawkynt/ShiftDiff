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

    [Fact]
    public void Fuzzy_HunkAtItsRecordedPosition_ReturnsExactConfidence()
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

        var result = PatchApplier.ApplyHunkFuzzy(source, hunk);

        Assert.Equal(new[] { "a", "B", "c" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.Exact, result.Confidence);
    }

    [Fact]
    public void Fuzzy_ContextShiftedByLineOffset_FindsItAndReturnsHighConfidence()
    {
        // Hunk header still claims the old block starts at line 1, but two
        // unrelated lines were prepended to the source, shifting the real
        // context down to line 3 — this is AC-005's "line offset drift".
        var source = new[] { "x", "y", "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var result = PatchApplier.ApplyHunkFuzzy(source, hunk);

        Assert.Equal(new[] { "x", "y", "a", "B", "c" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.High, result.Confidence);
    }

    [Fact]
    public void Fuzzy_ContextNotFoundAnywhereInSource_ThrowsPatchApplicationException()
    {
        var source = new[] { "x", "y", "z" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        Assert.Throws<PatchApplicationException>(() => PatchApplier.ApplyHunkFuzzy(source, hunk));
    }

    [Fact]
    public void Fuzzy_PureInsertionHunkWithNoOldLines_AppliesAtRecordedPositionAsExact()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(2, 0, 2, 1),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Added, "NEW"),
            });

        var result = PatchApplier.ApplyHunkFuzzy(source, hunk);

        Assert.Equal(new[] { "a", "NEW", "b", "c" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.Exact, result.Confidence);
    }

    [Fact]
    public void Fuzzy_MultiHunkFile_OneHunkShiftedAndOneExact_FileConfidenceIsHighOverall()
    {
        // First hunk's context ("a") only occurs at its recorded exact
        // position. Second hunk's context ("e") was pushed one line down by
        // an unrelated insertion, so it can only be found via drift search.
        var source = new[] { "a", "b", "c", "d", "z", "e" };
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

        var result = PatchApplier.ApplyFileFuzzy(source, file);

        Assert.Equal(new[] { "A", "b", "c", "d", "z", "E" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.High, result.Confidence);
    }
}
