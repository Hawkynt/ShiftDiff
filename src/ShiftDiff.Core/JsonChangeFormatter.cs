namespace ShiftDiff.Core;

public static class JsonChangeFormatter {
  public static IReadOnlyList<string> Format(JsonChange[] changes) {
    var lines = new List<string>();
    foreach (var change in changes) {
      if (change.ChangeType == JsonChangeType.Unchanged) {
        continue;
      }

      var path = change.Path ?? "(root)";
      var oldPart = change.OldValue is null ? string.Empty : $"{change.OldValue} ";
      var newPart = change.NewValue is null ? string.Empty : $" {change.NewValue}";
      lines.Add($"{path}: {change.ChangeType} {oldPart}->{newPart}");
    }

    return lines;
  }
}
