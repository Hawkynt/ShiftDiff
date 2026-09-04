namespace ShiftDiff.Core;

// FR-043 Visual Language: a shared marker vocabulary for every presenter (CLI, UI).
// Emoji are optional; text markers and labels always exist so nothing depends on
// emoji rendering alone.
public static class ChangeMarker {
  public static string Text(ChangeType changeType) => changeType switch {
    ChangeType.Unchanged => " ",
    ChangeType.Edited => "~",
    ChangeType.Added => "+",
    ChangeType.Removed => "-",
    ChangeType.Moved => "M",
    ChangeType.MovedEdited => "%",
    ChangeType.Split => "S",
    ChangeType.Merged => "J",
    ChangeType.Uncertain => "?",
    ChangeType.Conflict => "!",
    _ => " ",
  };

  public static string Emoji(ChangeType changeType) => changeType switch {
    ChangeType.Unchanged => "✅",
    ChangeType.Edited => "✏️",
    ChangeType.Added => "➕",
    ChangeType.Removed => "➖",
    ChangeType.Moved => "\U0001F69A",
    ChangeType.MovedEdited => "\U0001F500",
    ChangeType.Split => "\U0001F9E9",
    ChangeType.Merged => "\U0001F587️",
    ChangeType.Uncertain => "❓",
    ChangeType.Conflict => "⚠️",
    _ => "✅",
  };

  public static string Label(ChangeType changeType) => changeType switch {
    ChangeType.Unchanged => "unchanged",
    ChangeType.Edited => "edited",
    ChangeType.Added => "added",
    ChangeType.Removed => "removed",
    ChangeType.Moved => "moved",
    ChangeType.MovedEdited => "moved + edited",
    ChangeType.Split => "split",
    ChangeType.Merged => "merged",
    ChangeType.Uncertain => "uncertain",
    ChangeType.Conflict => "conflict",
    _ => "unchanged",
  };

  public static string Label(Confidence confidence) => confidence switch {
    Confidence.Certain => "certain",
    Confidence.Likely => "likely",
    Confidence.Possible => "possible",
    Confidence.Weak => "weak",
    Confidence.Rejected => "rejected",
    _ => "unknown",
  };

  public static string For(ChangeType changeType, bool useEmoji) =>
      useEmoji ? Emoji(changeType) : Text(changeType);
}
