# Comparison workspace and interactive merge

The desktop workspace compares two, three, or four file or folder sources in parallel. It is
inspired by mature merge tools without copying their ribbon density or making every operation
look like it launches a small satellite.

## Folder view

The upper pane aligns entries against the first source and classifies added, removed, changed,
moved, and moved-plus-edited files. When every file in a directory moves to the same directory,
the workspace also emits a folder-move relationship. A transparent relationship layer draws
the file and folder connections across panes.

Each pane can select a different file. This is intentional: a merge target may need a helper
method or configuration block from a file that does not correspond to the selected file in the
other panes.

## File and block view

The lower source panes show the independently selected files. ShiftDiff compares the first
available pane with every pane to its right, highlights changed ranges, and draws relationships
between moved blocks. Selecting a line selects the semantic block containing it; a single line
is used as a safe fallback when it belongs to no detected block.

## Interactive merge target

The merge target starts as a copy of the first available source file. Source blocks can be:

- inserted after the selected target line;
- used to replace the corresponding target range;
- taken from any pane and any file selected in that pane;
- undone without modifying source files.

Export writes a new reconstructed target through the platform save picker. It never overwrites a
source merely because someone clicked an arrow with excessive confidence.

## Themes

Light, system, and dark themes are directly available in the main toolbar. Layout surfaces use
theme resources; semantic colors use translucent overlays so their meaning remains consistent in
both light and dark modes.

