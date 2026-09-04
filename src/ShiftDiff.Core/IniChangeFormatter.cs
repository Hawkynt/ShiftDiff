namespace ShiftDiff.Core;

public static class IniChangeFormatter {
  public static IReadOnlyList<string> Format(IniChange[] changes) {
    var lines = new List<string>();
    foreach (var change in changes) {
      if (change.ChangeType == IniChangeType.Unchanged) {
        continue;
      }

      var oldPart = change.OldValue is null ? string.Empty : $"{change.OldValue} ";
      var newPart = change.NewValue is null ? string.Empty : $" {change.NewValue}";
      lines.Add($"{change.Path}: {change.ChangeType} {oldPart}->{newPart}");
    }

    return lines;
  }
}
