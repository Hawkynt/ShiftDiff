namespace ShiftDiff.Core;

public sealed record FileComparisonResult(LineChange[] Changes, BlockMatch[] MovedBlocks);

public static class FileComparer
{
    public static FileComparisonResult Compare(
        byte[] oldContent, byte[] newContent,
        bool ignoreCase = false, WhitespaceMode whitespaceMode = WhitespaceMode.None,
        DetectionMode mode = DetectionMode.Balanced)
    {
        var oldFile = TextFileLoader.Load(oldContent);
        var newFile = TextFileLoader.Load(newContent);

        var changes = LineDiffer.Diff(oldFile.Lines, newFile.Lines, ignoreCase, whitespaceMode);

        var candidates = BlockBuilder.Build(oldFile.Lines, newFile.Lines);
        var blockMatches = BlockClassifier.Classify(candidates, oldFile.Lines, newFile.Lines, mode);
        var movedBlocks = SplitMergeDetector.Detect(blockMatches);

        return new FileComparisonResult(changes, movedBlocks);
    }
}
