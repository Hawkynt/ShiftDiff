using System.Text;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class MarkdownMoveDetectorTests
{
    private static byte[] Bytes(string markdown) => Encoding.UTF8.GetBytes(markdown);

    [Fact]
    public void Detect_IdenticalBodyUnderRenamedHeading_MarksMovedWithMovedFrom()
    {
        var changes = MarkdownComparer.Compare(
            Bytes("# Old\ncontent\n"),
            Bytes("# New\ncontent\n"));

        var result = MarkdownMoveDetector.Detect(changes);

        var moved = Assert.Single(result);
        Assert.Equal(MarkdownChangeType.Moved, moved.ChangeType);
        Assert.Equal("# New", moved.Path);
        Assert.Equal("# Old", moved.MovedFrom);
    }

    [Fact]
    public void Detect_DifferentContent_NoMoveDetected_StaysAddedAndRemoved()
    {
        var changes = MarkdownComparer.Compare(
            Bytes("# Old\ncontent one\n"),
            Bytes("# New\ncontent two\n"));

        var result = MarkdownMoveDetector.Detect(changes);

        Assert.Equal(2, result.Length);
        Assert.Contains(result, c => c.Path == "# Old" && c.ChangeType == MarkdownChangeType.Removed);
        Assert.Contains(result, c => c.Path == "# New" && c.ChangeType == MarkdownChangeType.Added);
    }

    [Fact]
    public void Detect_AmbiguousMultipleCandidatesWithSameContent_LeavesAsAddedAndRemoved()
    {
        var changes = MarkdownComparer.Compare(
            Bytes("# A\nsame\n\n# B\nsame\n"),
            Bytes("# C\nsame\n"));

        var result = MarkdownMoveDetector.Detect(changes);

        Assert.Equal(3, result.Length);
        Assert.All(result, c => Assert.NotEqual(MarkdownChangeType.Moved, c.ChangeType));
    }

    [Fact]
    public void Detect_UnchangedAndChangedEntries_PassThroughUnaffected()
    {
        var changes = MarkdownComparer.Compare(
            Bytes("# Same\ntext\n\n# Edited\nold\n"),
            Bytes("# Same\ntext\n\n# Edited\nnew\n"));

        var result = MarkdownMoveDetector.Detect(changes);

        Assert.Equal(2, result.Length);
        Assert.Contains(result, c => c.Path == "# Same" && c.ChangeType == MarkdownChangeType.Unchanged);
        Assert.Contains(result, c => c.Path == "# Edited" && c.ChangeType == MarkdownChangeType.Changed);
    }

    [Fact]
    public void Detect_NoAddedOrRemoved_ReturnsInputUnchanged()
    {
        var changes = MarkdownComparer.Compare(
            Bytes("# Foo\nA\n"),
            Bytes("# Foo\nA\n"));

        var result = MarkdownMoveDetector.Detect(changes);

        Assert.Equal(changes, result);
    }

    [Fact]
    public void Detect_SimilarButEditedContentAtDifferentHeading_MarksMovedEditedWithMovedFrom()
    {
        var changes = MarkdownComparer.Compare(
            Bytes("# Old\noriginal body text here\n"),
            Bytes("# New\noriginal body text HERE-changed-a-bit\n"));

        var result = MarkdownMoveDetector.Detect(changes);

        var movedEdited = Assert.Single(result);
        Assert.Equal(MarkdownChangeType.MovedEdited, movedEdited.ChangeType);
        Assert.Equal("# New", movedEdited.Path);
        Assert.Equal("# Old", movedEdited.MovedFrom);
        Assert.Equal("original body text here", movedEdited.OldValue);
        Assert.Equal("original body text HERE-changed-a-bit", movedEdited.NewValue);
        Assert.NotNull(movedEdited.BodyChanges);
    }

    [Fact]
    public void Detect_AmbiguousMultipleFuzzyCandidates_LeavesAsAddedAndRemoved()
    {
        var changes = MarkdownComparer.Compare(
            Bytes("# A\noriginal body text here\n\n# B\noriginal body text here too\n"),
            Bytes("# C\noriginal body text HERE-changed-a-bit\n"));

        var result = MarkdownMoveDetector.Detect(changes);

        Assert.Equal(3, result.Length);
        Assert.All(result, c => Assert.NotEqual(MarkdownChangeType.MovedEdited, c.ChangeType));
    }

    [Fact]
    public void Detect_TwoUnrelatedEmptyBodySections_NotTreatedAsMoved()
    {
        var changes = MarkdownComparer.Compare(
            Bytes("# Alpha\n\n# Same\ntext\n"),
            Bytes("# Zulu\n\n# Same\ntext\n"));

        var result = MarkdownMoveDetector.Detect(changes);

        Assert.Equal(3, result.Length);
        Assert.Contains(result, c => c.Path == "# Alpha" && c.ChangeType == MarkdownChangeType.Removed);
        Assert.Contains(result, c => c.Path == "# Zulu" && c.ChangeType == MarkdownChangeType.Added);
        Assert.Contains(result, c => c.Path == "# Same" && c.ChangeType == MarkdownChangeType.Unchanged);
    }

    [Fact]
    public void Detect_TokenFreePunctuationOnlyBodies_NotTreatedAsMovedEdited()
    {
        var changes = MarkdownComparer.Compare(
            Bytes("# Alpha\n...\n\n# Same\ntext\n"),
            Bytes("# Zulu\n---\n\n# Same\ntext\n"));

        var result = MarkdownMoveDetector.Detect(changes);

        Assert.Equal(3, result.Length);
        Assert.Contains(result, c => c.Path == "# Alpha" && c.ChangeType == MarkdownChangeType.Removed);
        Assert.Contains(result, c => c.Path == "# Zulu" && c.ChangeType == MarkdownChangeType.Added);
    }
}
