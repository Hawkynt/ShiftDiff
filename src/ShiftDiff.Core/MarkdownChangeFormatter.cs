namespace ShiftDiff.Core;

public static class MarkdownChangeFormatter {
  public static IReadOnlyList<string> Format(MarkdownChange[] changes) {
    var lines = new List<string>();
    foreach (var change in changes) {
      if (change.ChangeType == MarkdownChangeType.Unchanged) {
        continue;
      }

      if (change.ChangeType == MarkdownChangeType.Moved) {
        lines.Add($"{change.Path}: Moved (from {change.MovedFrom})");
        continue;
      }

      if (change.ChangeType == MarkdownChangeType.MovedEdited) {
        lines.Add($"{change.Path}: MovedEdited (from {change.MovedFrom}) {change.OldValue} -> {change.NewValue}");
        continue;
      }

      var oldPart = change.OldValue is null ? string.Empty : $"{change.OldValue} ";
      var newPart = change.NewValue is null ? string.Empty : $" {change.NewValue}";
      lines.Add($"{change.Path}: {change.ChangeType} {oldPart}->{newPart}");
    }

    return lines;
  }
}
