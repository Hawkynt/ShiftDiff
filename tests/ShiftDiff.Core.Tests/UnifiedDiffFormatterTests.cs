using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class UnifiedDiffFormatterTests
{
    [Fact]
    public void SingleHunkSingleFile_RoundTripsThroughParser()
    {
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("old.txt", "new.txt"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 3, 1, 3),
                    new[]
                    {
                        new UnifiedDiffLine(UnifiedDiffLineKind.Context, "a"),
                        new UnifiedDiffLine(UnifiedDiffLineKind.Removed, "b"),
                        new UnifiedDiffLine(UnifiedDiffLineKind.Added, "b2"),
                    }),
            });

        var lines = UnifiedDiffFormatter.Format(file);
        var reparsed = UnifiedDiffParser.ParsePatch(lines.ToList());

        Assert.Single(reparsed.Files);
        AssertFilesEqual(file, reparsed.Files[0]);
    }

    [Fact]
    public void FileHeaderWithoutRevision_FormatsWithNoTrailingTab()
    {
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("old.txt", "new.txt"),
            Array.Empty<UnifiedDiffHunk>());

        var lines = UnifiedDiffFormatter.Format(file);

        Assert.Equal("--- old.txt", lines[0]);
        Assert.Equal("+++ new.txt", lines[1]);
    }

    [Fact]
    public void FileHeaderWithRevision_FormatsWithTabSeparatedRevision()
    {
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("old.txt", "new.txt", "2024-01-01 00:00:00", "2024-01-02 00:00:00"),
            Array.Empty<UnifiedDiffHunk>());

        var lines = UnifiedDiffFormatter.Format(file);

        Assert.Equal("--- old.txt\t2024-01-01 00:00:00", lines[0]);
        Assert.Equal("+++ new.txt\t2024-01-02 00:00:00", lines[1]);
    }

    [Fact]
    public void MultipleHunksInOneFile_FormatsEachHunkHeaderAndBody()
    {
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("old.txt", "new.txt"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 2, 1, 2),
                    new[]
                    {
                        new UnifiedDiffLine(UnifiedDiffLineKind.Context, "a"),
                        new UnifiedDiffLine(UnifiedDiffLineKind.Removed, "b"),
                    }),
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(10, 2, 10, 2),
                    new[]
                    {
                        new UnifiedDiffLine(UnifiedDiffLineKind.Context, "x"),
                        new UnifiedDiffLine(UnifiedDiffLineKind.Added, "y"),
                    }),
            });

        var lines = UnifiedDiffFormatter.Format(file);

        Assert.Equal(
            new[]
            {
                "--- old.txt", "+++ new.txt",
                "@@ -1,2 +1,2 @@", " a", "-b",
                "@@ -10,2 +10,2 @@", " x", "+y",
            },
            lines);
    }

    [Fact]
    public void MultipleFilesInOnePatch_FormatsEachFileBackToBack()
    {
        var patch = new UnifiedDiffPatch(new[]
        {
            new UnifiedDiffFile(
                new UnifiedDiffFileHeader("a/old1.txt", "b/new1.txt"),
                new[]
                {
                    new UnifiedDiffHunk(
                        new UnifiedDiffHunkHeader(1, 2, 1, 2),
                        new[]
                        {
                            new UnifiedDiffLine(UnifiedDiffLineKind.Context, "a"),
                            new UnifiedDiffLine(UnifiedDiffLineKind.Removed, "b"),
                        }),
                }),
            new UnifiedDiffFile(
                new UnifiedDiffFileHeader("a/old2.txt", "b/new2.txt"),
                new[]
                {
                    new UnifiedDiffHunk(
                        new UnifiedDiffHunkHeader(10, 2, 10, 2),
                        new[]
                        {
                            new UnifiedDiffLine(UnifiedDiffLineKind.Context, "x"),
                            new UnifiedDiffLine(UnifiedDiffLineKind.Added, "y"),
                        }),
                }),
        });

        var lines = UnifiedDiffFormatter.Format(patch);
        var reparsed = UnifiedDiffParser.ParsePatch(lines.ToList());

        Assert.Equal(patch.Files.Count, reparsed.Files.Count);
        for (var i = 0; i < patch.Files.Count; i++)
        {
            AssertFilesEqual(patch.Files[i], reparsed.Files[i]);
        }
    }

    private static void AssertFilesEqual(UnifiedDiffFile expected, UnifiedDiffFile actual)
    {
        Assert.Equal(expected.Header, actual.Header);
        Assert.Equal(expected.Hunks.Count, actual.Hunks.Count);
        for (var i = 0; i < expected.Hunks.Count; i++)
        {
            Assert.Equal(expected.Hunks[i].Header, actual.Hunks[i].Header);
            Assert.Equal(expected.Hunks[i].Lines, actual.Hunks[i].Lines);
        }
    }

    [Fact]
    public void FileWithGitHeader_ThrowsNotSupportedException()
    {
        var lines = new[]
        {
            "diff --git a/foo.txt b/foo.txt",
            "--- a/foo.txt", "+++ b/foo.txt",
            "@@ -1,1 +1,1 @@", " a",
        };
        var file = UnifiedDiffParser.ParseFile(lines);

        Assert.NotNull(file.GitHeader);
        Assert.Throws<NotSupportedException>(() => UnifiedDiffFormatter.Format(file));
    }
}
