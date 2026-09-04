using ShiftDiff.Vcs;

namespace ShiftDiff.Vcs.Tests;

public class SvnProviderTests : IDisposable {
  private const string StatusXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <status>
          <target path=".">
            <entry path="src/File.cs">
              <wc-status item="modified" revision="1200" props="none" />
            </entry>
            <entry path="src/New.cs">
              <wc-status item="added" revision="0" props="none" />
            </entry>
            <entry path="src/Gone.cs">
              <wc-status item="deleted" revision="1200" props="none" />
            </entry>
            <entry path="notes.txt">
              <wc-status item="unversioned" props="none" />
            </entry>
          </target>
        </status>
        """;

  private readonly string _root = Path.Combine(Path.GetTempPath(), "shiftdiff-vcs", Guid.NewGuid().ToString("N"));

  public SvnProviderTests() => Directory.CreateDirectory(_root);

  public void Dispose() {
    if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
  }

  [Fact]
  public void Detect_WorkingCopyWithDotSvn_ReturnsTheWorkingCopyRoot() {
    Directory.CreateDirectory(Path.Combine(_root, ".svn"));
    var nested = Directory.CreateDirectory(Path.Combine(_root, "trunk", "src")).FullName;

    var info = new SvnProvider(new FakeProcessRunner()).Detect(nested);

    Assert.NotNull(info);
    Assert.Equal(VcsKind.Svn, info!.Kind);
    Assert.Equal(Path.GetFullPath(_root), Path.GetFullPath(info.Root));
  }

  [Fact]
  public void Detect_PathWithoutWorkingCopy_ReturnsNull() {
    Assert.Null(new SvnProvider(new FakeProcessRunner()).Detect(_root));
  }

  [Fact]
  public void GetWorkingChanges_ParsesTheXmlStatusIntoLocalModifications() {
    var runner = new FakeProcessRunner().Respond("status", StatusXml);

    var changes = new SvnProvider(runner).GetWorkingChanges(_root);

    Assert.Equal(4, changes.Count);
    Assert.Equal(VcsChangeKind.Modified, changes[0].Kind);
    Assert.Equal(VcsChangeKind.Added, changes[1].Kind);
    Assert.Equal(VcsChangeKind.Deleted, changes[2].Kind);
    Assert.Equal(VcsChangeKind.Untracked, changes[3].Kind);
    Assert.Contains("--xml", runner.LastCommandLine);
  }

  [Fact]
  public void GetChanges_BetweenRevisions_PassesARevisionRange() {
    var runner = new FakeProcessRunner().Respond("diff", StatusXml);

    new SvnProvider(runner).GetChanges(_root, "1200", "1250");

    Assert.Contains("1200:1250", runner.LastCommandLine);
    Assert.Contains("--summarize", runner.LastCommandLine);
  }

  [Fact]
  public void GetFileContent_AtRevision_UsesSvnCat() {
    var runner = new FakeProcessRunner().Respond("cat", "old content\n");

    var content = new SvnProvider(runner).GetFileContent(_root, "src/File.cs", "1200");

    Assert.Equal("old content\n", content);
    Assert.Contains("cat -r 1200 src/File.cs", runner.LastCommandLine);
  }

  [Fact]
  public void GetFileContent_WorkingCopyRevision_ReadsFromDisk() {
    File.WriteAllText(Path.Combine(_root, "live.cs"), "working copy");

    Assert.Equal("working copy", new SvnProvider(new FakeProcessRunner()).GetFileContent(_root, "live.cs", ""));
  }

  [Fact]
  public void GetHistory_ParsesTheXmlLog() {
    const string logXml = """
            <?xml version="1.0"?>
            <log>
              <logentry revision="1250">
                <author>ada</author>
                <date>2024-05-01T10:00:00.000000Z</date>
                <msg>Add the thing</msg>
              </logentry>
            </log>
            """;
    var runner = new FakeProcessRunner().Respond("log", logXml);

    var revision = Assert.Single(new SvnProvider(runner).GetHistory(_root));

    Assert.Equal("1250", revision.Id);
    Assert.Equal("ada", revision.Author);
    Assert.Equal("Add the thing", revision.Message);
  }

  [Fact]
  public void GetUnifiedDiff_ReturnsRawSvnDiffOutputForTheParser() {
    var runner = new FakeProcessRunner().Respond("diff", "Index: file.cs\n===\n@@ -1 +1 @@\n-a\n+b\n");

    var diff = new SvnProvider(runner).GetUnifiedDiff(_root, "1200:1250");

    Assert.Contains("Index: file.cs", diff);
  }

  [Fact]
  public void FailingCommand_RaisesAVcsCommandException() {
    var runner = new FakeProcessRunner().RespondWithFailure("status", "svn: E155007: not a working copy");

    var exception = Assert.Throws<VcsCommandException>(() => new SvnProvider(runner).GetWorkingChanges(_root));

    Assert.Contains("not a working copy", exception.Message);
  }

  [Fact]
  public void ParseStatus_NonXmlFallback_StillReadsThePlainStatusCodes() {
    var changes = SvnOutputParser.ParseStatus("M       src/File.cs\nA       src/New.cs\n?       notes.txt\n");

    Assert.Equal(3, changes.Count);
    Assert.Equal(VcsChangeKind.Modified, changes[0].Kind);
    Assert.Equal("src/File.cs", changes[0].Path);
  }

  [Fact]
  public void ParseStatus_EmptyOutput_YieldsNoChanges() {
    Assert.Empty(SvnOutputParser.ParseStatus(string.Empty));
  }

  [Fact]
  public void ParseLog_MalformedXml_YieldsNoRevisions() {
    Assert.Empty(SvnOutputParser.ParseLog("<log>"));
  }
}
