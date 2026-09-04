using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

public class SourceLanguageDetectorTests {
  [Theory]
  [InlineData("sample.cs", SourceLanguage.CSharp)]
  [InlineData("sample.pl", SourceLanguage.Perl)]
  [InlineData("sample.py", SourceLanguage.Python)]
  [InlineData("sample.php", SourceLanguage.Php)]
  [InlineData("sample.go", SourceLanguage.Go)]
  [InlineData("sample.rs", SourceLanguage.Rust)]
  [InlineData("sample.c", SourceLanguage.C)]
  [InlineData("sample.cpp", SourceLanguage.Cpp)]
  [InlineData("sample.vb", SourceLanguage.VisualBasic)]
  [InlineData("sample.rb", SourceLanguage.Ruby)]
  [InlineData("sample.tsx", SourceLanguage.TypeScript)]
  [InlineData("sample.java", SourceLanguage.Java)]
  [InlineData("sample.sql", SourceLanguage.Sql)]
  public void Detect_MapsKnownExtensions(string path, SourceLanguage expected) {
    Assert.Equal(expected, SourceLanguageDetector.Detect(path));
  }

  [Theory]
  [InlineData("#!/usr/bin/env python3", SourceLanguage.Python)]
  [InlineData("#!/usr/bin/perl", SourceLanguage.Perl)]
  [InlineData("#!/usr/bin/env ruby", SourceLanguage.Ruby)]
  [InlineData("#!/usr/bin/env node", SourceLanguage.JavaScript)]
  public void Detect_UsesShebangWhenExtensionIsUnknown(string content, SourceLanguage expected) {
    Assert.Equal(expected, SourceLanguageDetector.Detect("script", content));
  }

  [Fact]
  public void DetectCommon_UsesKnownLanguageWhenOtherSideHasNoExtension() {
    var language = SourceLanguageDetector.DetectCommon("old.cs", "class A {}", "temporary", "class B {}");

    Assert.Equal(SourceLanguage.CSharp, language);
  }

  [Fact]
  public void DetectCommon_FallsBackToPlainTextForDifferentKnownLanguages() {
    var language = SourceLanguageDetector.DetectCommon("old.cs", "class A {}", "new.py", "class B: pass");

    Assert.Equal(SourceLanguage.PlainText, language);
  }
}

