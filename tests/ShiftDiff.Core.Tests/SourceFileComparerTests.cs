using System.Text;
using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

public class SourceFileComparerTests {
  [Fact]
  public void CompareSourceFiles_DetectsLanguageAndUsesLanguageAwareStringTokens() {
    var oldContent = Encoding.UTF8.GetBytes("var value = \"foo\";\n");
    var newContent = Encoding.UTF8.GetBytes("var value = \"bar\";\n");

    var result = FileComparer.CompareSourceFiles(oldContent, newContent, "old.cs", "new.cs");

    Assert.Equal(SourceLanguage.CSharp, result.Language);
    var edited = Assert.Single(result.Comparison.Changes, change => change.ChangeType == ChangeType.Edited);
    Assert.Contains(edited.TokenChanges!, change => change.OldToken == "\"foo\"");
    Assert.Contains(edited.TokenChanges!, change => change.NewToken == "\"bar\"");
  }

  [Fact]
  public void CompareSourceFiles_KeepsExistingComparisonOptions() {
    var oldContent = Encoding.UTF8.GetBytes("Public Sub Run()\n");
    var newContent = Encoding.UTF8.GetBytes("public sub Run()\n");

    var result = FileComparer.CompareSourceFiles(oldContent, newContent, "old.vb", "new.vb", ignoreCase: true);

    Assert.Equal(SourceLanguage.VisualBasic, result.Language);
    Assert.All(result.Comparison.Changes, change => Assert.Equal(ChangeType.Unchanged, change.ChangeType));
  }
}

