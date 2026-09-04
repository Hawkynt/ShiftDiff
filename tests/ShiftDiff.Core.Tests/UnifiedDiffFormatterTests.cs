using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class UnifiedDiffFormatterTests {
  [Fact]
  public void SingleHunkSingleFile_RoundTripsThroughParser() {
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
  public void FileHeaderWithoutRevision_FormatsWithNoTrailingTab() {
    var file = new UnifiedDiffFile(
        new UnifiedDiffFileHeader("old.txt", "new.txt"),
        Array.Empty<UnifiedDiffHunk>());

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal("--- old.txt", lines[0]);
    Assert.Equal("+++ new.txt", lines[1]);
  }

  [Fact]
  public void FileHeaderWithRevision_FormatsWithTabSeparatedRevision() {
    var file = new UnifiedDiffFile(
        new UnifiedDiffFileHeader("old.txt", "new.txt", "2024-01-01 00:00:00", "2024-01-02 00:00:00"),
        Array.Empty<UnifiedDiffHunk>());

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal("--- old.txt\t2024-01-01 00:00:00", lines[0]);
    Assert.Equal("+++ new.txt\t2024-01-02 00:00:00", lines[1]);
  }

  [Fact]
  public void MultipleHunksInOneFile_FormatsEachHunkHeaderAndBody() {
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
  public void HunkSubsetSelectedViaWithExpression_OnlyEmitsSelectedHunk() {
    // FR-024 "selected changes only" export: no dedicated API needed — a
    // caller filters UnifiedDiffFile.Hunks down to the desired subset via
    // a `with` expression before calling Format. Locks in the 798-dream
    // finding with an actual regression test (previously assessed, never
    // verified by a test).
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

    var selectedFile = file with { Hunks = new[] { file.Hunks[1] } };
    var lines = UnifiedDiffFormatter.Format(selectedFile);

    Assert.Equal(
        new[] { "--- old.txt", "+++ new.txt", "@@ -10,2 +10,2 @@", " x", "+y" },
        lines);
  }

  [Fact]
  public void MultipleFilesInOnePatch_FormatsEachFileBackToBack() {
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
    for (var i = 0; i < patch.Files.Count; i++) {
      AssertFilesEqual(patch.Files[i], reparsed.Files[i]);
    }
  }

  private static void AssertFilesEqual(UnifiedDiffFile expected, UnifiedDiffFile actual) {
    Assert.Equal(expected.Header, actual.Header);
    Assert.Equal(expected.GitHeader, actual.GitHeader);
    Assert.Equal(expected.Hunks.Count, actual.Hunks.Count);
    for (var i = 0; i < expected.Hunks.Count; i++) {
      Assert.Equal(expected.Hunks[i].Header, actual.Hunks[i].Header);
      Assert.Equal(expected.Hunks[i].Lines, actual.Hunks[i].Lines);
    }
  }

  [Fact]
  public void GitFileWithContentHunks_RoundTripsFullyWithDashPairAndHunks() {
    var lines = new[]
    {
            "diff --git a/foo.txt b/foo.txt",
            "index abc123..def456 100644",
            "--- a/foo.txt", "+++ b/foo.txt",
            "@@ -1,1 +1,1 @@", " a",
        };
    var file = UnifiedDiffParser.ParseFile(lines);

    var formatted = UnifiedDiffFormatter.Format(file);
    Assert.Equal(lines, formatted);

    var reparsed = UnifiedDiffParser.ParsePatch(formatted.ToList());
    Assert.Single(reparsed.Files);
    AssertFilesEqual(file, reparsed.Files[0]);
  }

  [Fact]
  public void GitModeChange_FormatsOldModeThenNewModeWithNoDashPair() {
    var file = new UnifiedDiffFile(
        new UnifiedDiffFileHeader("foo.txt", "foo.txt"),
        Array.Empty<UnifiedDiffHunk>(),
        new GitExtendedHeader(
            new GitDiffHeader("foo.txt", "foo.txt"),
            new GitFileModeChange("100644", "100755"),
            null, null, null, null));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(
        new[] { "diff --git a/foo.txt b/foo.txt", "old mode 100644", "new mode 100755" },
        lines);
    RoundTripsThroughParser(file);
  }

  [Fact]
  public void GitNewFileMode_FormatsNewFileModeLine() {
    var file = GitOnlyFile(creationMode: new GitFileCreationMode(GitFileCreationKind.NewFile, "100644"));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(new[] { "diff --git a/new.txt b/new.txt", "new file mode 100644" }, lines);
    RoundTripsThroughParser(file);
  }

  [Fact]
  public void GitDeletedFileMode_FormatsDeletedFileModeLine() {
    var file = GitOnlyFile(creationMode: new GitFileCreationMode(GitFileCreationKind.DeletedFile, "100644"));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(new[] { "diff --git a/new.txt b/new.txt", "deleted file mode 100644" }, lines);
    RoundTripsThroughParser(file);
  }

  [Fact]
  public void GitSimilarityIndex_FormatsSimilarityIndexLine() {
    var file = GitOnlyFile(similarityIndex: new GitSimilarityIndex(GitSimilarityKind.Similarity, 90));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(new[] { "diff --git a/new.txt b/new.txt", "similarity index 90%" }, lines);
    RoundTripsThroughParser(file);
  }

  [Fact]
  public void GitDissimilarityIndex_FormatsDissimilarityIndexLine() {
    var file = GitOnlyFile(similarityIndex: new GitSimilarityIndex(GitSimilarityKind.Dissimilarity, 10));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(new[] { "diff --git a/new.txt b/new.txt", "dissimilarity index 10%" }, lines);
    RoundTripsThroughParser(file);
  }

  [Fact]
  public void GitRenameMetadata_FormatsRenameFromAndRenameToLinesWithNoDashPair() {
    // The parser derives Header from rename metadata (not GitHeader.Header) when there
    // are no hunks, so Header must match SourcePath/TargetPath for a clean round-trip.
    var file = new UnifiedDiffFile(
        new UnifiedDiffFileHeader("old/path.txt", "new/path.txt"),
        Array.Empty<UnifiedDiffHunk>(),
        new GitExtendedHeader(
            new GitDiffHeader("new.txt", "new.txt"),
            null, null, null,
            new GitRenameCopyMetadata(GitRenameCopyKind.Rename, "old/path.txt", "new/path.txt"),
            null));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(
        new[] { "diff --git a/new.txt b/new.txt", "rename from old/path.txt", "rename to new/path.txt" },
        lines);
    RoundTripsThroughParser(file);
  }

  [Fact]
  public void GitCopyMetadata_FormatsCopyFromAndCopyToLines() {
    var file = new UnifiedDiffFile(
        new UnifiedDiffFileHeader("old/path.txt", "new/path.txt"),
        Array.Empty<UnifiedDiffHunk>(),
        new GitExtendedHeader(
            new GitDiffHeader("new.txt", "new.txt"),
            null, null, null,
            new GitRenameCopyMetadata(GitRenameCopyKind.Copy, "old/path.txt", "new/path.txt"),
            null));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(
        new[] { "diff --git a/new.txt b/new.txt", "copy from old/path.txt", "copy to new/path.txt" },
        lines);
    RoundTripsThroughParser(file);
  }

  [Fact]
  public void GitIndexHashWithoutMode_FormatsIndexLine() {
    var file = GitOnlyFile(indexHash: new GitIndexHash("abc123", "def456", null));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(new[] { "diff --git a/new.txt b/new.txt", "index abc123..def456" }, lines);
    RoundTripsThroughParser(file);
  }

  [Fact]
  public void GitIndexHashWithMode_FormatsIndexLineWithTrailingMode() {
    var file = GitOnlyFile(indexHash: new GitIndexHash("abc123", "def456", "100644"));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(new[] { "diff --git a/new.txt b/new.txt", "index abc123..def456 100644" }, lines);
    RoundTripsThroughParser(file);
  }

  [Fact]
  public void GitHeaderWithAllOptionalComponents_EmitsThemInParseOrder() {
    var file = new UnifiedDiffFile(
        new UnifiedDiffFileHeader("old/path.txt", "new/path.txt"),
        Array.Empty<UnifiedDiffHunk>(),
        new GitExtendedHeader(
            new GitDiffHeader("old/path.txt", "new/path.txt"),
            new GitFileModeChange("100644", "100755"),
            new GitFileCreationMode(GitFileCreationKind.NewFile, "100644"),
            new GitSimilarityIndex(GitSimilarityKind.Similarity, 90),
            new GitRenameCopyMetadata(GitRenameCopyKind.Rename, "old/path.txt", "new/path.txt"),
            new GitIndexHash("abc123", "def456", "100644")));

    var lines = UnifiedDiffFormatter.Format(file);

    Assert.Equal(
        new[]
        {
                "diff --git a/old/path.txt b/new/path.txt",
                "old mode 100644", "new mode 100755",
                "similarity index 90%",
                "rename from old/path.txt", "rename to new/path.txt",
                "index abc123..def456 100644",
        },
        lines);
  }

  [Fact]
  public void SvnFileWithRevisions_FormatsIndexHeaderSeparatorAndBody() {
    var file = new UnifiedDiffFile(
        new UnifiedDiffFileHeader("old.txt", "old.txt", "(revision 5)", "(working copy)"),
        new[]
        {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 1, 1, 1),
                    new[] { new UnifiedDiffLine(UnifiedDiffLineKind.Context, "a") }),
        });

    var lines = UnifiedDiffFormatter.FormatSvn(file);

    Assert.Equal(
        new[]
        {
                "Index: old.txt",
                new string('=', 67),
                "--- old.txt\t(revision 5)",
                "+++ old.txt\t(working copy)",
                "@@ -1,1 +1,1 @@", " a",
        },
        lines);
  }

  [Fact]
  public void SvnFileWithoutRevision_FormatsDashPairWithNoTrailingTab() {
    var file = new UnifiedDiffFile(
        new UnifiedDiffFileHeader("old.txt", "old.txt"),
        Array.Empty<UnifiedDiffHunk>());

    var lines = UnifiedDiffFormatter.FormatSvn(file);

    Assert.Equal(
        new[] { "Index: old.txt", new string('=', 67), "--- old.txt", "+++ old.txt" },
        lines);
  }

  [Fact]
  public void SvnFileWithGitHeader_ThrowsNotSupportedException() {
    var file = GitOnlyFile(indexHash: new GitIndexHash("abc123", "def456", null));

    Assert.Throws<NotSupportedException>(() => UnifiedDiffFormatter.FormatSvn(file));
  }

  [Fact]
  public void SvnPatch_MultipleFiles_FormatsEachFileBackToBack() {
    var patch = new UnifiedDiffPatch(new[]
    {
            new UnifiedDiffFile(
                new UnifiedDiffFileHeader("old1.txt", "old1.txt"),
                Array.Empty<UnifiedDiffHunk>()),
            new UnifiedDiffFile(
                new UnifiedDiffFileHeader("old2.txt", "old2.txt"),
                Array.Empty<UnifiedDiffHunk>()),
        });

    var lines = UnifiedDiffFormatter.FormatSvn(patch);

    Assert.Equal(
        new[]
        {
                "Index: old1.txt", new string('=', 67), "--- old1.txt", "+++ old1.txt",
                "Index: old2.txt", new string('=', 67), "--- old2.txt", "+++ old2.txt",
        },
        lines);
  }

  private static UnifiedDiffFile GitOnlyFile(
      GitFileModeChange? modeChange = null,
      GitFileCreationMode? creationMode = null,
      GitSimilarityIndex? similarityIndex = null,
      GitRenameCopyMetadata? renameCopyMetadata = null,
      GitIndexHash? indexHash = null) {
    return new UnifiedDiffFile(
        new UnifiedDiffFileHeader("new.txt", "new.txt"),
        Array.Empty<UnifiedDiffHunk>(),
        new GitExtendedHeader(
            new GitDiffHeader("new.txt", "new.txt"),
            modeChange, creationMode, similarityIndex, renameCopyMetadata, indexHash));
  }

  private static void RoundTripsThroughParser(UnifiedDiffFile file) {
    var lines = UnifiedDiffFormatter.Format(file);
    var reparsed = UnifiedDiffParser.ParsePatch(lines.ToList());

    Assert.Single(reparsed.Files);
    AssertFilesEqual(file, reparsed.Files[0]);
  }
}
