using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

public class InteractiveMergeDocumentTests {
  [Fact]
  public void Insert_AcceptsABlockFromAnUnrelatedSourceFile() {
    var document = new InteractiveMergeDocument(["target-a", "target-b"]);
    var block = new MergeSourceBlock("pane-3", "helpers/other.cs", 10, 11, ["helper-a", "helper-b"]);

    var edit = document.Insert(block, 1);

    Assert.Equal(["target-a", "helper-a", "helper-b", "target-b"], document.Lines);
    Assert.Equal("helpers/other.cs", edit.Block.SourcePath);
    Assert.Equal(MergeEditKind.Insert, edit.Kind);
  }

  [Fact]
  public void Replace_ReplacesTheSelectedTargetRangeWithTheWholeBlock() {
    var document = new InteractiveMergeDocument(["keep", "old-a", "old-b", "tail"]);
    var block = new MergeSourceBlock("pane-2", "new.cs", 4, 6, ["new-a", "new-b", "new-c"]);

    document.Replace(block, 1, 2);

    Assert.Equal(["keep", "new-a", "new-b", "new-c", "tail"], document.Lines);
  }

  [Fact]
  public void Apply_DoesNotMutateTheSourceBlock() {
    var sourceLines = new[] { "source-a", "source-b" };
    var block = new MergeSourceBlock("pane", "source.cs", 0, 1, sourceLines);
    var document = new InteractiveMergeDocument(["target"]);

    document.Insert(block, 0);
    document.Replace(new MergeSourceBlock("other", "other.cs", 0, 0, ["replacement"]), 0, 1);

    Assert.Equal(["source-a", "source-b"], sourceLines);
    Assert.Equal(["source-a", "source-b"], block.Lines);
  }

  [Fact]
  public void Undo_RestoresTheDocumentBeforeTheLastEdit() {
    var document = new InteractiveMergeDocument(["a", "b"]);
    document.Insert(new MergeSourceBlock("pane", "file.cs", 0, 0, ["x"]), 1);

    var undone = document.Undo();

    Assert.True(undone);
    Assert.Equal(["a", "b"], document.Lines);
    Assert.False(document.CanUndo);
  }

  [Fact]
  public void Undo_WithNoHistory_ReturnsFalse() {
    var document = new InteractiveMergeDocument([]);

    Assert.False(document.Undo());
  }
}

