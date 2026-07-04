using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class UnifiedDiffParserTests
{
    [Fact]
    public void BothCountsExplicit_ParsesAllFourValues()
    {
        var header = UnifiedDiffParser.ParseHunkHeader("@@ -1,5 +1,6 @@");
        Assert.Equal(1, header.OldStart);
        Assert.Equal(5, header.OldCount);
        Assert.Equal(1, header.NewStart);
        Assert.Equal(6, header.NewCount);
    }

    [Fact]
    public void OmittedCount_DefaultsToOne()
    {
        var header = UnifiedDiffParser.ParseHunkHeader("@@ -1 +1 @@");
        Assert.Equal(1, header.OldStart);
        Assert.Equal(1, header.OldCount);
        Assert.Equal(1, header.NewStart);
        Assert.Equal(1, header.NewCount);
    }

    [Fact]
    public void ZeroOldCount_ParsesNewFileAddition()
    {
        var header = UnifiedDiffParser.ParseHunkHeader("@@ -0,0 +1,3 @@");
        Assert.Equal(0, header.OldStart);
        Assert.Equal(0, header.OldCount);
        Assert.Equal(1, header.NewStart);
        Assert.Equal(3, header.NewCount);
    }

    [Fact]
    public void TrailingSectionHeading_IsIgnored()
    {
        var header = UnifiedDiffParser.ParseHunkHeader("@@ -10,3 +10,4 @@ public void Foo()");
        Assert.Equal(10, header.OldStart);
        Assert.Equal(3, header.OldCount);
        Assert.Equal(10, header.NewStart);
        Assert.Equal(4, header.NewCount);
    }

    [Fact]
    public void MissingHunkMarkers_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseHunkHeader("not a hunk header"));
    }

    [Fact]
    public void PlainPaths_NoSuffix_ParsesBothPaths()
    {
        var header = UnifiedDiffParser.ParseFileHeader("--- old.txt", "+++ new.txt");
        Assert.Equal("old.txt", header.SourcePath);
        Assert.Equal("new.txt", header.TargetPath);
        Assert.Null(header.SourceRevision);
        Assert.Null(header.TargetRevision);
    }

    [Fact]
    public void TabSeparatedTimestampSuffix_IsStrippedFromPathAndExposedAsRevision()
    {
        var header = UnifiedDiffParser.ParseFileHeader(
            "--- old.txt\t2024-01-01 00:00:00",
            "+++ new.txt\t2024-01-02 00:00:00");
        Assert.Equal("old.txt", header.SourcePath);
        Assert.Equal("new.txt", header.TargetPath);
        Assert.Equal("2024-01-01 00:00:00", header.SourceRevision);
        Assert.Equal("2024-01-02 00:00:00", header.TargetRevision);
    }

    [Fact]
    public void OnlyOldLineHasRevisionSuffix_TargetRevisionIsNull()
    {
        var header = UnifiedDiffParser.ParseFileHeader("--- old.txt\trev1", "+++ new.txt");
        Assert.Equal("rev1", header.SourceRevision);
        Assert.Null(header.TargetRevision);
    }

    [Fact]
    public void GitStylePrefix_IsKeptVerbatim()
    {
        var header = UnifiedDiffParser.ParseFileHeader("--- a/src/Foo.cs", "+++ b/src/Foo.cs");
        Assert.Equal("a/src/Foo.cs", header.SourcePath);
        Assert.Equal("b/src/Foo.cs", header.TargetPath);
    }

    [Fact]
    public void MalformedOldLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseFileHeader("old.txt", "+++ new.txt"));
    }

    [Fact]
    public void MalformedNewLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseFileHeader("--- old.txt", "new.txt"));
    }

    [Fact]
    public void AddedLine_ParsesKindAndContent()
    {
        var line = UnifiedDiffParser.ParseLine("+new text");
        Assert.Equal(UnifiedDiffLineKind.Added, line.Kind);
        Assert.Equal("new text", line.Content);
    }

    [Fact]
    public void RemovedLine_ParsesKindAndContent()
    {
        var line = UnifiedDiffParser.ParseLine("-old text");
        Assert.Equal(UnifiedDiffLineKind.Removed, line.Kind);
        Assert.Equal("old text", line.Content);
    }

    [Fact]
    public void ContextLine_ParsesKindAndContent()
    {
        var line = UnifiedDiffParser.ParseLine(" unchanged text");
        Assert.Equal(UnifiedDiffLineKind.Context, line.Kind);
        Assert.Equal("unchanged text", line.Content);
    }

    [Fact]
    public void BareMarker_ParsesEmptyContent()
    {
        var line = UnifiedDiffParser.ParseLine("+");
        Assert.Equal(UnifiedDiffLineKind.Added, line.Kind);
        Assert.Equal("", line.Content);
    }

    [Fact]
    public void UnrecognizedPrefix_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseLine("@oops"));
    }

    [Fact]
    public void EmptyString_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseLine(""));
    }

    [Fact]
    public void AllContextHunk_ParsesHeaderAndAllContextLines()
    {
        var hunk = UnifiedDiffParser.ParseHunk("@@ -1,3 +1,3 @@", new[] { " a", " b", " c" });
        Assert.Equal(1, hunk.Header.OldStart);
        Assert.Equal(3, hunk.Header.OldCount);
        Assert.Equal(3, hunk.Lines.Count);
        Assert.All(hunk.Lines, line => Assert.Equal(UnifiedDiffLineKind.Context, line.Kind));
    }

    [Fact]
    public void MixedHunk_ParsesLinesInOrderWithCorrectKinds()
    {
        var hunk = UnifiedDiffParser.ParseHunk("@@ -1,2 +1,3 @@", new[] { " same", "-old", "+new1", "+new2" });
        Assert.Equal(4, hunk.Lines.Count);
        Assert.Equal(
            new[]
            {
                UnifiedDiffLineKind.Context,
                UnifiedDiffLineKind.Removed,
                UnifiedDiffLineKind.Added,
                UnifiedDiffLineKind.Added,
            },
            hunk.Lines.Select(line => line.Kind));
    }

    [Fact]
    public void EmptyBody_ParsesEmptyLinesCollection()
    {
        var hunk = UnifiedDiffParser.ParseHunk("@@ -1,0 +1,0 @@", Array.Empty<string>());
        Assert.Empty(hunk.Lines);
    }

    [Fact]
    public void MalformedHeader_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseHunk("not a hunk header", Array.Empty<string>()));
    }

    [Fact]
    public void MalformedBodyLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseHunk("@@ -1,1 +1,1 @@", new[] { "@oops" }));
    }

    [Fact]
    public void SingleHunk_MatchesStandaloneParseHunkResult()
    {
        var lines = new[] { "--- old.txt", "+++ new.txt", "@@ -1,3 +1,3 @@", " a", " b", " c" };
        var file = UnifiedDiffParser.ParseFile(lines);
        var expectedHunk = UnifiedDiffParser.ParseHunk("@@ -1,3 +1,3 @@", new[] { " a", " b", " c" });
        Assert.Single(file.Hunks);
        Assert.Equal(expectedHunk.Header, file.Hunks[0].Header);
        Assert.Equal(expectedHunk.Lines, file.Hunks[0].Lines);
    }

    [Fact]
    public void MultipleHunks_ParsedInOrderWithCorrectContent()
    {
        var lines = new[]
        {
            "--- old.txt", "+++ new.txt",
            "@@ -1,2 +1,2 @@", " a", "-b",
            "@@ -10,2 +10,2 @@", " x", "+y",
        };
        var file = UnifiedDiffParser.ParseFile(lines);
        Assert.Equal(2, file.Hunks.Count);
        Assert.Equal(1, file.Hunks[0].Header.OldStart);
        Assert.Equal(2, file.Hunks[0].Lines.Count);
        Assert.Equal(10, file.Hunks[1].Header.OldStart);
        Assert.Equal(2, file.Hunks[1].Lines.Count);
    }

    [Fact]
    public void HeaderOnly_NoHunks_ParsesEmptyHunksList()
    {
        var lines = new[] { "--- old.txt", "+++ new.txt" };
        var file = UnifiedDiffParser.ParseFile(lines);
        Assert.Empty(file.Hunks);
    }

    [Fact]
    public void ContentAfterHeaderIsNotAHunkHeader_ThrowsFormatException()
    {
        var lines = new[] { "--- old.txt", "+++ new.txt", " not a hunk header" };
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseFile(lines));
    }

    [Fact]
    public void MalformedFileHeader_PropagatesFormatException()
    {
        var lines = new[] { "old.txt", "+++ new.txt", "@@ -1,1 +1,1 @@", " a" };
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseFile(lines));
    }

    [Fact]
    public void SingleFile_MatchesStandaloneParseFileResult()
    {
        var lines = new[] { "--- old.txt", "+++ new.txt", "@@ -1,3 +1,3 @@", " a", " b", " c" };
        var patch = UnifiedDiffParser.ParsePatch(lines);
        var expectedFile = UnifiedDiffParser.ParseFile(lines);
        Assert.Single(patch.Files);
        Assert.Equal(expectedFile.Header, patch.Files[0].Header);
        Assert.Equal(expectedFile.Hunks.Count, patch.Files[0].Hunks.Count);
        Assert.Equal(expectedFile.Hunks[0].Header, patch.Files[0].Hunks[0].Header);
        Assert.Equal(expectedFile.Hunks[0].Lines, patch.Files[0].Hunks[0].Lines);
    }

    [Fact]
    public void MultipleFiles_ParsedInOrderWithCorrectPaths()
    {
        var lines = new[]
        {
            "--- a/old1.txt", "+++ b/new1.txt",
            "@@ -1,2 +1,2 @@", " a", "-b",
            "--- a/old2.txt", "+++ b/new2.txt",
            "@@ -10,2 +10,2 @@", " x", "+y",
        };
        var patch = UnifiedDiffParser.ParsePatch(lines);
        Assert.Equal(2, patch.Files.Count);
        Assert.Equal("a/old1.txt", patch.Files[0].Header.SourcePath);
        Assert.Equal("b/new1.txt", patch.Files[0].Header.TargetPath);
        Assert.Equal("a/old2.txt", patch.Files[1].Header.SourcePath);
        Assert.Equal("b/new2.txt", patch.Files[1].Header.TargetPath);
    }

    [Fact]
    public void EmptyInput_ParsesEmptyFilesCollection()
    {
        var patch = UnifiedDiffParser.ParsePatch(Array.Empty<string>());
        Assert.Empty(patch.Files);
    }

    [Fact]
    public void FirstLineNotAFileHeader_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParsePatch(new[] { "@@ -1,1 +1,1 @@", " a" }));
    }

    [Fact]
    public void SecondFileMissingNewLine_PropagatesFormatException()
    {
        var lines = new[]
        {
            "--- a/old1.txt", "+++ b/new1.txt",
            "@@ -1,1 +1,1 @@", " a",
            "--- a/old2.txt", "@@ -1,1 +1,1 @@", " a",
        };
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParsePatch(lines));
    }

    [Fact]
    public void NormalPair_ParsesOldAndNewModes()
    {
        var change = UnifiedDiffParser.ParseFileModeChange("old mode 100644", "new mode 100755");
        Assert.Equal("100644", change.OldMode);
        Assert.Equal("100755", change.NewMode);
    }

    [Fact]
    public void MalformedOldModeLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseFileModeChange("100644", "new mode 100755"));
    }

    [Fact]
    public void MalformedNewModeLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseFileModeChange("old mode 100644", "100755"));
    }

    [Fact]
    public void NewFileLine_ParsesNewFileKindAndMode()
    {
        var creation = UnifiedDiffParser.ParseFileCreationMode("new file mode 100644");
        Assert.Equal(GitFileCreationKind.NewFile, creation.Kind);
        Assert.Equal("100644", creation.Mode);
    }

    [Fact]
    public void DeletedFileLine_ParsesDeletedFileKindAndMode()
    {
        var creation = UnifiedDiffParser.ParseFileCreationMode("deleted file mode 100644");
        Assert.Equal(GitFileCreationKind.DeletedFile, creation.Kind);
        Assert.Equal("100644", creation.Mode);
    }

    [Fact]
    public void UnrecognizedCreationLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseFileCreationMode("100644"));
    }

    [Fact]
    public void SimilarityIndexLine_ParsesSimilarityKindAndPercentage()
    {
        var index = UnifiedDiffParser.ParseSimilarityIndex("similarity index 100%");
        Assert.Equal(GitSimilarityKind.Similarity, index.Kind);
        Assert.Equal(100, index.Percentage);
    }

    [Fact]
    public void DissimilarityIndexLine_ParsesDissimilarityKindAndPercentage()
    {
        var index = UnifiedDiffParser.ParseSimilarityIndex("dissimilarity index 45%");
        Assert.Equal(GitSimilarityKind.Dissimilarity, index.Kind);
        Assert.Equal(45, index.Percentage);
    }

    [Fact]
    public void SimilarityIndexLine_MissingPercentSign_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseSimilarityIndex("similarity index 100"));
    }

    [Fact]
    public void UnrecognizedSimilarityIndexLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseSimilarityIndex("100%"));
    }

    [Fact]
    public void RenamePair_ParsesRenameKindAndPaths()
    {
        var metadata = UnifiedDiffParser.ParseRenameCopyMetadata("rename from old.txt", "rename to new.txt");
        Assert.Equal(GitRenameCopyKind.Rename, metadata.Kind);
        Assert.Equal("old.txt", metadata.SourcePath);
        Assert.Equal("new.txt", metadata.TargetPath);
    }

    [Fact]
    public void CopyPair_ParsesCopyKindAndPaths()
    {
        var metadata = UnifiedDiffParser.ParseRenameCopyMetadata("copy from old.txt", "copy to new.txt");
        Assert.Equal(GitRenameCopyKind.Copy, metadata.Kind);
        Assert.Equal("old.txt", metadata.SourcePath);
        Assert.Equal("new.txt", metadata.TargetPath);
    }

    [Fact]
    public void MalformedFromLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseRenameCopyMetadata("old.txt", "rename to new.txt"));
    }

    [Fact]
    public void MismatchedKindBetweenFromAndToLines_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UnifiedDiffParser.ParseRenameCopyMetadata("rename from old.txt", "copy to new.txt"));
    }
}
