# Comparison workspace and interactive merge

The desktop application is a thin shell over `ShiftDiff.Ui`: the shell view model owns the
session, the document model and the navigation state, and the Avalonia window only translates
gestures into calls and state into controls. Everything described here is unit-tested without a
display.

## Sessions

A session is opened by dropping paths on the window, by the toolbar buttons, or from the command
line:

| Dropped               | Session                                                        |
| --------------------- | -------------------------------------------------------------- |
| two files             | side-by-side comparison                                        |
| three files           | base/local/remote three-pane merge preview                     |
| four files            | base/local/remote plus the reconstructed target (AC-003)       |
| two folders           | folder comparison with move, copy and rename detection         |
| three or four folders | workspace comparison (`ComparisonWorkspace`) across all sources |
| one folder            | Git or SVN working copy, if one is detected there              |

Folder and repository sessions fill the file-list sidebar; picking an entry compares that file.

## Panes

Rows are aligned across every pane, so scrolling is synchronized by construction. Each row shows
its change marker, per-pane line numbers, and the line itself split into runs that carry both the
diff state (added/removed inside an edited line) and the syntax class of the token. Runs of
unchanged lines further than the context distance from a change fold into a single expandable
row.

A relocated block is drawn as one moved unit on both sides — never as a delete on the left and an
add on the right — and a relationship thread connects its two ends across the panes.

## Overview bar and navigation

The overview bar compresses the whole document into stripes coloured by change type, with a
viewport indicator; clicking or dragging jumps there. The toolbar and keyboard cover next and
previous change (F7/F8), next conflict (Shift+F8), next moved block (Ctrl+F8) and jump to the
paired end of a moved block (Ctrl+P).

## Repository sessions

Opening a working copy compares HEAD (or BASE for SVN) against the working tree.
The revision fields in the options bar compare any other range — two commits,
two tags, a branch against the working tree — and the file list refreshes with
whatever changed between them.

## Inspector

Selecting a line explains it: change type, source and target line, and — for a block match — its
range, size, similarity score, confidence and the reason the engine accepted the match (FR-046).
The moved-block list beneath jumps to any block.

## Interactive merge target

The reconstructed result always mirrors the second pane — the target file of a two-way
comparison, the local file of a three-way merge — so a resolution is always "use that version
instead". `Take left` (or `Take base` and `Take remote` in a merge) replaces the selected change
run, `Undo` reverts the last action, and `Save…` writes the result. Source files are never
modified, and an existing file is never overwritten without an explicit confirmation (AC-010).

A four-way comparison validates a candidate target rather than producing one, so it offers no
resolution actions.

## Themes and markers

Light, dark and system themes are declared as theme dictionaries, so every surface and every
semantic colour switches together. Change markers are available as emoji or as plain text and can
be switched at any time; no marker relies on colour or emoji alone (FR-043/FR-044). A
high-contrast mode strengthens every semantic fill, text zoom is available from the options bar
or with Ctrl+plus/minus, and long lines can be wrapped instead of scrolled.
