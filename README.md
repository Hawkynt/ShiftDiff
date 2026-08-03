# ShiftDiff

[![License](https://img.shields.io/github/license/Hawkynt/ShiftDiff)](https://github.com/Hawkynt/ShiftDiff/blob/main/LICENSE)
[![Language](https://img.shields.io/github/languages/top/Hawkynt/ShiftDiff?color=8957D5)](https://github.com/Hawkynt/ShiftDiff)

[![CI](https://github.com/Hawkynt/ShiftDiff/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Hawkynt/ShiftDiff/actions/workflows/ci.yml)
![Last Commit](https://img.shields.io/github/last-commit/Hawkynt/ShiftDiff?branch=main)
![Activity](https://img.shields.io/github/commit-activity/m/Hawkynt/ShiftDiff)

[![Stars](https://img.shields.io/github/stars/Hawkynt/ShiftDiff?color=FFD700)](https://github.com/Hawkynt/ShiftDiff/stargazers)
[![Forks](https://img.shields.io/github/forks/Hawkynt/ShiftDiff?color=008080)](https://github.com/Hawkynt/ShiftDiff/network/members)
[![Issues](https://img.shields.io/github/issues/Hawkynt/ShiftDiff)](https://github.com/Hawkynt/ShiftDiff/issues)
![Code Size](https://img.shields.io/github/languages/code-size/Hawkynt/ShiftDiff?color=4CAF50)
![Repo Size](https://img.shields.io/github/repo-size/Hawkynt/ShiftDiff?color=FF9800)

[![Release](https://img.shields.io/github/v/release/Hawkynt/ShiftDiff)](https://github.com/Hawkynt/ShiftDiff/releases/latest)
[![Nightly](https://img.shields.io/github/v/release/Hawkynt/ShiftDiff?include_prereleases&sort=date&filter=nightly-*&label=nightly&color=FF9800)](https://github.com/Hawkynt/ShiftDiff/releases)
[![Downloads](https://img.shields.io/github/downloads/Hawkynt/ShiftDiff/total)](https://github.com/Hawkynt/ShiftDiff/releases)

> Semantic diff viewer for moved, edited, merged, and reconstructed content.

Traditional diff tools are line-oriented: a block of code that moved shows up
as a delete at the old spot and an add at the new one, and a large refactor
becomes an unreadable wall of red and green. ShiftDiff instead treats a diff as
a *relationship* problem — it tries to answer "where did this block go, and
what happened to it on the way" instead of just "what lines differ."

Full spec (goals, use cases, UI, roadmap): [SPEC.md](SPEC.md).

## Desktop showcase

The same three-folder comparison is captured from the real Avalonia application
in CI. The upper workspace aligns moved files and folders; the lower workspace
shows moved/edited blocks and the block-editable merge target.

### Dark theme

[![ShiftDiff three-pane folder and source comparison in dark theme](docs/screenshots/workspace-dark.png)](docs/screenshots/workspace-dark.png)

### Light theme

[![ShiftDiff three-pane folder and source comparison in light theme](docs/screenshots/workspace-light.png)](docs/screenshots/workspace-light.png)

The deterministic inputs live in [`docs/showcase`](docs/showcase), and
[`scripts/capture-showcase.sh`](scripts/capture-showcase.sh) regenerates both
screenshots through the UI Showcase workflow.

## Example

![shiftdiff comparing two files, one method reordered and edited](docs/example.png)

A method (`Validate`/`Describe`) was reordered and its condition edited. This
is the CLI's current default output — a plain unified diff via
`FileComparer`/`UnifiedDiffFormatter`. The move/edit-aware classification
described below (`BlockClassifier`, `Confidence`) is implemented and tested in
`ShiftDiff.Core`, but isn't wired into the CLI's output yet, so a moved method
still renders as delete+add here rather than as an annotated move.

## How it works

The diff engine (`ShiftDiff.Core`) runs a normalize → hash → anchor → block →
score → classify pipeline:

1. **Line hashing** (`LineHasher`) — every line gets four hashes: raw,
   trimmed, whitespace-normalized, and whitespace-stripped ("token-normalized").
   This lets later stages match lines despite reformatting.
2. **Anchor detection** (`AnchorDetector`) — lines are graded by how
   trustworthy they are as a matching anchor (uniqueness, frequency, length),
   so low-value lines like `{`, `}`, blank lines, or repeated boilerplate
   can't single-handedly create a false match.
3. **Block building** (`BlockBuilder`) — contiguous runs of matching lines
   between the old and new file become candidate blocks.
4. **Similarity scoring** (`BlockSimilarityScorer`) — each candidate gets a
   combined score from eight independent signals: exact/normalized hash
   overlap, token-shingle Jaccard similarity, SimHash fingerprint similarity,
   block size ratio, ordering consistency, rarity-weighted anchor score, and
   neighboring-block consistency.
5. **Classification** (`BlockClassifier`) — each candidate becomes one
   `ChangeType`: `Unchanged`, `Edited`, `Added`, `Removed`, `Moved`,
   `MovedEdited`, `Split`, `Merged`, or `Uncertain` (via `SplitMergeDetector`
   for the split/merge cases), each carrying a `Confidence`
   (`Certain`/`Likely`/`Possible`/`Weak`/`Rejected`, from `ConfidenceClassifier`).
6. **Detection mode** (`DetectionMode`: `Strict`/`Balanced`/`Aggressive`) tunes
   the thresholds — minimum block size, minimum token count, maximum
   duplicate-anchor frequency, and the score cutoff between a pure move and a
   moved+edited block.

## Patch engine

`UnifiedDiffParser` / `UnifiedDiffFormatter` / `PatchApplier` round-trip
unified diffs, including Git's extended headers (mode changes, renames,
copies, similarity index, `diff --git` paths) and SVN-style diff export:

- **Parse** a unified/Git/SVN patch into structured hunks and file metadata.
- **Apply** a patch in exact mode (context must match verbatim), fuzzy mode
  (search nearby for a matching position when the recorded line offset is
  stale), or semantic mode (match by block identity instead of line number).
- **Export** the (possibly reconstructed) result back to a unified diff,
  a Git-compatible patch, or an SVN-compatible diff.

## Status

The diff/patch **engine** (`ShiftDiff.Core`) implements the pipeline above —
line hashing, anchor detection, block building/scoring/classification, split
merge detection, and unified/Git/SVN patch parsing + exact/fuzzy/semantic
application + export.

The **CLI** (`ShiftDiff.Cli`) can run a two-way compare (plain unified diff),
a three-way base/local/remote merge (conflict markers on unresolved hunks),
and apply a unified diff patch to a source file — but it calls the plain line
differ directly, not the semantic engine, so it doesn't yet surface moved
blocks or confidence levels (see the Example above).

The **desktop UI** (`ShiftDiff.App`) now provides a two-to-four-pane file and folder
workspace, drag-and-drop/open loading, move relationship lines, change navigation,
interactive block insertion/replacement, undo, safe export, and direct
light/system/dark theme controls. Source files are detected and tokenized using
language profiles for C#, JavaScript/TypeScript, Java, C/C++, Python, Go, Rust,
PHP, Perl, Ruby, Visual Basic, HTML, CSS, and SQL.

**Not built yet:** direct Git/SVN repository integration and a fully editable
character-level merge target. See SPEC.md §8.4/§12 for the planned CLI surface
and §17 for MVP scope.

## Layout

- `src/ShiftDiff.Core` — diff engine (hashing, anchor detection, block
  matching, patch parsing/application). No UI or VCS dependencies.
- `src/ShiftDiff.Cli` — command-line entry point (two-way/three-way compare, patch apply).
- `src/ShiftDiff.App` — Avalonia desktop UI for two-to-four-pane file/folder comparison and block merging.
- `tests/ShiftDiff.Core.Tests` — xunit tests for the core engine.
- `tests/ShiftDiff.Cli.Tests` — xunit tests for the CLI.

Language profiles live in `ShiftDiff.Core` and are independent of the Avalonia UI.
See [source language support](docs/source-language-support.md) for the extension model.
The VCS-integration project remains planned.

## Building

```
dotnet build
dotnet test
dotnet run --project src/ShiftDiff.App -- path-a path-b [path-c] [path-d]
```

## Workflow

TDD/BDD/DDD/SDD: every feature starts from a spec requirement (FR-xxx/AC-xxx
in SPEC.md), gets a failing test named after the behavior it pins, then the
implementation that turns it green. Domain terms in code match the spec's
vocabulary (block, anchor, hunk, confidence, role, etc.).

## ❤️ Support

If this project saves you time or money, consider supporting its development:

[![GitHub Sponsors](https://img.shields.io/badge/GitHub-Sponsor-EA4AAA?logo=githubsponsors)](https://github.com/sponsors/Hawkynt)
[![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?logo=paypal)](https://www.paypal.me/hawkynt)

## License

Licensed under LGPL-3.0-or-later — see [LICENSE](LICENSE).
