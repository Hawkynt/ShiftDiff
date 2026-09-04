using System.Text;

namespace ShiftDiff.Core;

public sealed record FileComparisonResult(LineChange[] Changes, BlockMatch[] MovedBlocks);

public sealed record SourceFileComparisonResult(SourceLanguage Language, FileComparisonResult Comparison);

public static class FileComparer {
  public static FileComparisonResult Compare(
      byte[] oldContent, byte[] newContent,
      bool ignoreCase = false, WhitespaceMode whitespaceMode = WhitespaceMode.None,
      DetectionMode mode = DetectionMode.Balanced, bool isSourceCode = false) =>
      CompareCore(oldContent, newContent, ignoreCase, whitespaceMode, mode, isSourceCode ? SourceLanguage.CSharp : null);

  public static SourceFileComparisonResult CompareSourceFiles(
      byte[] oldContent,
      byte[] newContent,
      string? oldPath,
      string? newPath,
      bool ignoreCase = false,
      WhitespaceMode whitespaceMode = WhitespaceMode.None,
      DetectionMode mode = DetectionMode.Balanced) {
    var oldText = DecodeForDetection(oldContent);
    var newText = DecodeForDetection(newContent);
    var language = SourceLanguageDetector.DetectCommon(oldPath, oldText, newPath, newText);
    var comparison = CompareCore(oldContent, newContent, ignoreCase, whitespaceMode, mode, language);
    return new SourceFileComparisonResult(language, comparison);
  }

  private static FileComparisonResult CompareCore(
      byte[] oldContent,
      byte[] newContent,
      bool ignoreCase,
      WhitespaceMode whitespaceMode,
      DetectionMode mode,
      SourceLanguage? language) {
    var oldFile = TextFileLoader.Load(oldContent);
    var newFile = TextFileLoader.Load(newContent);

    // An empty file loads as a single empty line; comparing that against a
    // real file would report one edit instead of a file full of added lines.
    var oldLines = Lines(oldFile);
    var newLines = Lines(newFile);

    var changes = LineDiffer.Diff(oldLines, newLines, ignoreCase, whitespaceMode)
        .Select(change => change.ChangeType == ChangeType.Edited
            ? change with {
              TokenChanges = language is null
                    ? TokenDiffer.Diff(change.OldLine!, change.NewLine!, ignoreCase, whitespaceMode)
                    : TokenDiffer.Diff(change.OldLine!, change.NewLine!, language.Value, ignoreCase, whitespaceMode),
            }
            : change)
        .ToArray();

    var candidates = BlockBuilder.Build(oldLines, newLines);
    var blockMatches = BlockClassifier.Classify(candidates, oldLines, newLines, mode);
    var movedBlocks = SplitMergeDetector.Detect(blockMatches);

    return new FileComparisonResult(changes, movedBlocks);
  }

  private static string[] Lines(TextFileContent file) =>
      file.Lines is [] or [""] ? [] : file.Lines;

  private static string DecodeForDetection(byte[] content) {
    if (content.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) return Encoding.UTF8.GetString(content, 3, content.Length - 3);
    if (content.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE })) return Encoding.Unicode.GetString(content, 2, content.Length - 2);
    if (content.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF })) return Encoding.BigEndianUnicode.GetString(content, 2, content.Length - 2);
    return Encoding.UTF8.GetString(content);
  }
}
