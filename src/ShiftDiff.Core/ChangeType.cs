namespace ShiftDiff.Core;

public enum ChangeType {
  Unchanged,
  Edited,
  Added,
  Removed,
  Moved,
  MovedEdited,
  Split,
  Merged,
  Uncertain,
  Conflict
}
