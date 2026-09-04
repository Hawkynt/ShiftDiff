using ShiftDiff.Vcs;

namespace ShiftDiff.Vcs.Tests;

public class GitOutputParserTests {
  [Fact]
  public void ParseStatus_ModifiedInWorkTree_IsUnstagedModification() {
    var status = Assert.Single(GitOutputParser.ParseStatus(" M src/File.cs\n"));

    Assert.Equal("src/File.cs", status.Path);
    Assert.Equal(VcsChangeKind.Modified, status.Kind);
    Assert.False(status.Staged);
  }

  [Fact]
  public void ParseStatus_ModifiedInIndex_IsStagedModification() {
    var status = Assert.Single(GitOutputParser.ParseStatus("M  src/File.cs\n"));

    Assert.Equal(VcsChangeKind.Modified, status.Kind);
    Assert.True(status.Staged);
  }

  [Fact]
  public void ParseStatus_StagedAndThenModifiedAgain_ReportsBothSides() {
    var statuses = GitOutputParser.ParseStatus("MM src/File.cs\n");

    Assert.Equal(2, statuses.Count);
    Assert.Contains(statuses, s => s.Staged);
    Assert.Contains(statuses, s => !s.Staged);
  }

  [Fact]
  public void ParseStatus_Untracked_IsReportedAsUntracked() {
    var status = Assert.Single(GitOutputParser.ParseStatus("?? new.txt\n"));

    Assert.Equal(VcsChangeKind.Untracked, status.Kind);
    Assert.Equal("new.txt", status.Path);
  }

  [Fact]
  public void ParseStatus_Ignored_IsReportedAsIgnored() {
    Assert.Equal(VcsChangeKind.Ignored, Assert.Single(GitOutputParser.ParseStatus("!! bin/app.dll\n")).Kind);
  }

  [Theory]
  [InlineData("UU merged.txt")]
  [InlineData("AA merged.txt")]
  [InlineData("DD merged.txt")]
  [InlineData("AU merged.txt")]
  public void ParseStatus_UnmergedCodes_AreReportedAsConflicted(string line) {
    Assert.Equal(VcsChangeKind.Conflicted, Assert.Single(GitOutputParser.ParseStatus(line)).Kind);
  }

  [Fact]
  public void ParseStatus_Rename_KeepsBothPaths() {
    var status = Assert.Single(GitOutputParser.ParseStatus("R  old/name.cs -> new/name.cs\n"));

    Assert.Equal(VcsChangeKind.Renamed, status.Kind);
    Assert.Equal("new/name.cs", status.Path);
    Assert.Equal("old/name.cs", status.OriginalPath);
  }

  [Fact]
  public void ParseStatus_QuotedPath_IsUnquoted() {
    var status = Assert.Single(GitOutputParser.ParseStatus(" M \"path with space.cs\"\n"));

    Assert.Equal("path with space.cs", status.Path);
  }

  [Fact]
  public void ParseStatus_EmptyOutput_YieldsNoChanges() {
    Assert.Empty(GitOutputParser.ParseStatus(string.Empty));
  }

  [Fact]
  public void ParseStatus_TooShortLine_IsIgnoredRatherThanCrashing() {
    Assert.Empty(GitOutputParser.ParseStatus("M\n"));
  }

  [Fact]
  public void ParseNameStatus_AddedAndDeleted_AreMapped() {
    var statuses = GitOutputParser.ParseNameStatus("A\tadded.cs\nD\tgone.cs\nM\tedited.cs\n");

    Assert.Equal(3, statuses.Count);
    Assert.Equal(VcsChangeKind.Added, statuses[0].Kind);
    Assert.Equal(VcsChangeKind.Deleted, statuses[1].Kind);
    Assert.Equal(VcsChangeKind.Modified, statuses[2].Kind);
  }

  [Fact]
  public void ParseNameStatus_RenameWithSimilarity_KeepsOriginAndScore() {
    var status = Assert.Single(GitOutputParser.ParseNameStatus("R096\told.cs\tnew.cs\n"));

    Assert.Equal(VcsChangeKind.Renamed, status.Kind);
    Assert.Equal("new.cs", status.Path);
    Assert.Equal("old.cs", status.OriginalPath);
    Assert.Equal(96, status.SimilarityPercentage);
  }

  [Fact]
  public void ParseNameStatus_Copy_KeepsSourcePath() {
    var status = Assert.Single(GitOutputParser.ParseNameStatus("C100\tsource.cs\tcopy.cs\n"));

    Assert.Equal(VcsChangeKind.Copied, status.Kind);
    Assert.Equal("source.cs", status.OriginalPath);
  }

  [Fact]
  public void ParseNameStatus_StagedFlag_IsPropagated() {
    Assert.True(Assert.Single(GitOutputParser.ParseNameStatus("M\tf.cs", staged: true)).Staged);
  }

  [Fact]
  public void ParseLog_FourFieldRecords_BecomeRevisions() {
    var separator = GitOutputParser.LogFieldSeparator;
    var output = $"abc123{separator}Ada{separator}2024-05-01T10:00:00+02:00{separator}Add the thing\n"
               + $"def456{separator}Grace{separator}2024-04-30T09:00:00+02:00{separator}Remove the thing\n";

    var revisions = GitOutputParser.ParseLog(output);

    Assert.Equal(2, revisions.Count);
    Assert.Equal("abc123", revisions[0].Id);
    Assert.Equal("Ada", revisions[0].Author);
    Assert.Equal("Add the thing", revisions[0].Message);
    Assert.Equal(2024, revisions[0].Timestamp.Year);
  }

  [Fact]
  public void ParseLog_MalformedRecord_IsSkipped() {
    Assert.Empty(GitOutputParser.ParseLog("not-a-record\n"));
  }
}
