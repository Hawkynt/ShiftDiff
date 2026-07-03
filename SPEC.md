# Product Requirements Document: Semantic Multi-File Diff Viewer

## 1. Product Name

ShiftDiff

Working subtitle: Semantic diff viewer for moved, edited, merged, and reconstructed content.

## 2. Purpose

ShiftDiff is a cross-platform semantic diff and merge viewer that compares up to four files at once, detects moved blocks even when they were subtly changed, provides a clean visual interface for humans, and can reconstruct target files from diffs or patch data.

The product is intended for developers, reviewers, technical writers, build engineers, release managers, and anyone who has ever stared at a diff and thought: "No, you stupid rectangle, this method was moved, not deleted."

## 3. Problem Statement

Traditional diff tools are line-oriented. They are good at showing additions and deletions, but bad at recognizing intent.

Common failures:

- Moved blocks are shown as delete + add.
- Slightly edited moved blocks lose their identity.
- Large refactorings become unreadable noise.
- Multi-file comparisons are clumsy or unsupported.
- Patch reconstruction is often bolted on instead of being core functionality.
- Git and SVN integrations are either too shallow or too tool-specific.
- UI is often either ugly, overloaded, or both. Sometimes impressively both.

ShiftDiff should make complex diffs understandable by identifying relationships between content blocks, not merely line positions.

## 4. Goals

### 4.1 Core Goals

- Compare 2, 3, or 4 files simultaneously.
- Detect:
  - unchanged blocks
  - edited blocks
  - moved blocks
  - moved + edited blocks
  - added blocks
  - removed blocks
  - split blocks
  - merged blocks
- Use semantic and fuzzy matching to identify moved blocks even with subtle modifications.
- Provide a fast, native-feeling, cross-platform UI.
- Support drag-and-drop loading of files, folders, patches, and repository objects.
- Use visual hints, icons, and emojis to improve clarity without turning the interface into a sticker collection.
- Reconstruct target files from diffs where enough information exists.
- Support Git and SVN directly.
- Provide a clean extension model for additional VCS providers and file formats.

### 4.2 Non-Goals for Initial Release

- Full IDE replacement.
- Full semantic understanding of every programming language.
- Perfect merge conflict resolution.
- Distributed collaboration or live multi-user editing.
- Cloud storage, accounts, telemetry-first nonsense.
- AI-generated merge decisions in the MVP.

## 5. Target Platforms

ShiftDiff must support:

- Windows 10+
- macOS 13+
- Linux desktop distributions using modern GTK/KDE environments

Preferred delivery formats:

- Windows: ".msi", ".zip", optional Microsoft Store later
- macOS: ".dmg", notarized app bundle
- Linux: ".AppImage", ".deb", ".rpm", optional Flatpak

## 6. Target Users

### 6.1 Primary Users

**Software Developers** — Need to review refactors, branch changes, generated files, patches, and merge conflicts.

**Code Reviewers** — Need a readable diff that separates real changes from file movement and formatting churn.

**Release Engineers** — Need to compare generated artifacts, vendor drops, release branches, and patch sets.

**Technical Writers** — Need to compare structured documentation, Markdown, XML, JSON, YAML, and configuration files.

### 6.2 Secondary Users

- QA engineers
- DevOps engineers
- Legal/compliance users comparing contracts or policy documents
- Database migration authors
- Localization teams

## 7. Key Use Cases

### 7.1 Two-Way File Comparison

User compares "old.cs" and "new.cs". The tool identifies that a method moved from the bottom of the file to the top and was slightly edited.

Expected behavior:

- Show the method as moved + edited, not deleted + added.
- Provide a jump link between original and new location.
- Show internal token/line diff inside the moved block.

### 7.2 Three-Way Merge View

User compares: base file, local file, remote file.

Expected behavior:

- Detect local-only changes.
- Detect remote-only changes.
- Detect overlapping changes.
- Detect blocks moved independently on both sides.
- Allow constructing a resolved target file.

### 7.3 Four-Way Comparison

User compares: base, local, remote, resolved / candidate / generated output.

Expected behavior:

- Show all four files in synchronized panes.
- Allow toggling visible panes.
- Highlight how the resolved output relates to base/local/remote.
- Detect whether the fourth file correctly incorporates selected changes.

### 7.4 Patch Application and Target Reconstruction

User opens: source file, unified diff / Git patch / SVN diff.

Expected behavior:

- Parse diff hunks.
- Apply hunks using exact and fuzzy matching.
- Construct target file.
- Show uncertain hunks requiring user confirmation.
- Export reconstructed target file.

### 7.5 Repository Comparison

User opens a Git or SVN repository.

Expected behavior:

- Compare:
  - working tree vs HEAD
  - branch vs branch
  - commit vs commit
  - revision vs revision
  - staged vs unstaged
  - file history versions
- Display changed files.
- Open selected files in semantic diff view.

### 7.6 Drag-and-Drop Workflow

User drags files into the window.

Expected behavior:

- One file dropped: wait for more input or offer comparison targets.
- Two files dropped: open two-way comparison.
- Three files dropped: open three-way comparison.
- Four files dropped: open four-way comparison.
- Folder dropped: offer recursive folder comparison.
- Patch dropped: offer patch preview/application.
- Repository folder dropped: detect Git/SVN metadata.

## 8. Functional Requirements

### 8.1 File Comparison

**FR-001: Two-Way Diff** — The application shall compare two files and show differences using synchronized panes.

Required features: line-level diff, token-level inline diff, moved block detection, whitespace-ignore modes, case-ignore mode, line ending normalization, encoding detection, binary file detection.

**FR-002: Three-Way Diff** — The application shall compare base, local, and remote files.

Required features: base/local/remote layout, conflict detection, independent change detection, local-only and remote-only markers, merge preview, resolved output construction.

**FR-003: Four-Way Diff** — The application shall compare up to four files in one session.

Supported layouts: 2-pane, 3-pane, 4-pane grid, 4-pane horizontal, focused pane with mini-overview.

The fourth file may represent: resolved output, generated target, candidate output, another branch/revision, patched file.

**FR-004: Folder Comparison** — The application shall support recursive folder comparison.

Required features: added/removed/changed/moved files, rename detection, copy detection where feasible, filtering by extension/path/size/ignore rules, opening individual file diffs from folder view.

**FR-005: Binary File Handling** — The application shall detect binary files.

For binary files, it shall show: file size, hash, modified timestamp where available, byte-level equality, optional hex diff in advanced mode.

Semantic moved-block detection is not required for binary files.

### 8.2 Semantic Block Detection

**FR-010: Line Hashing** — The application shall compute normalized hashes for each line. At minimum: raw hash, trimmed hash, whitespace-normalized hash, token-normalized hash where possible.

**FR-011: Anchor Detection** — The application shall detect anchor lines using line hashes.

Anchor quality shall consider: uniqueness, frequency, line length, token richness, indentation-only noise, known boilerplate patterns.

Common low-value lines such as "{", "}", blank lines, "else", and import separators shall not be treated as strong anchors.

**FR-012: Block Construction** — The application shall build candidate moved blocks from matching anchor lines.

Block growth shall allow: small insertions, small deletions, substitutions, bounded gaps, shifted line positions, formatting-only changes.

The algorithm shall tolerate up to configurable mismatch budgets.

**FR-013: Moved Block Detection** — The application shall classify matched blocks as: unchanged, edited, moved, moved + edited, split, merged, uncertain.

A moved block shall be detected even if: comments change, whitespace changed, a few lines changed, identifiers changed mildly, surrounding context changed.

**FR-014: Block Similarity Scoring** — The application shall compute block similarity using a weighted score.

Recommended components: exact line hash overlap, normalized line hash overlap, token shingle similarity, SimHash or MinHash similarity, block size ratio, ordering consistency, rarity-weighted anchor score, neighboring block consistency.

**FR-015: Confidence Values** — Every semantic match shall have a confidence score.

Example levels: Certain, Likely, Possible, Weak, Rejected.

The UI shall expose low-confidence matches differently from high-confidence matches.

**FR-016: False Positive Controls** — The application shall avoid over-aggressive move detection.

Controls: minimum block size, minimum token count, maximum size ratio difference, maximum duplicate anchor frequency, configurable similarity threshold, "strict"/"balanced"/"aggressive" modes.

Default mode shall be balanced.

### 8.3 Patch and Target Reconstruction

**FR-020: Patch Parsing** — The application shall parse: unified diff, Git patch, SVN diff, context diff where feasible.

It shall extract: source path, target path, old revision, new revision, hunks, added lines, removed lines, context lines, file mode changes where supported, rename/copy metadata where supported.

**FR-021: Patch Application** — The application shall apply diffs to source files.

Application modes: exact, fuzzy, semantic.

Exact mode requires context to match exactly. Fuzzy mode allows line offset and minor context drift. Semantic mode may apply hunks based on block identity rather than raw line number.

**FR-022: Target File Construction** — The application shall construct target files from: source file + patch, base + local + remote merge decisions, selected diff changes, repository revision deltas.

The user shall be able to export constructed files.

**FR-023: Patch Conflict Handling** — If patch application is ambiguous, the application shall: mark hunk as uncertain, show candidate locations, explain why confidence is low, allow user to select target location, allow user to skip hunk, allow manual editing.

**FR-024: Patch Export** — The application shall export: unified diff, Git-compatible patch, optional SVN-compatible diff where feasible, resolved target file, selected changes only.

### 8.4 Version Control Integration

**FR-030: Git Support** — The application shall support Git repositories directly.

Required Git features: detect repository root, show working tree changes, show staged changes, compare commits, compare branches, compare tags, compare file against HEAD, compare file across history, show renames, show copies where Git detects them, open conflicted files in 3-way mode, stage/unstage selected files or hunks (optional for v1.1), create patch from selected changes.

Implementation may call Git CLI initially, with optional libgit2 backend later.

**FR-031: SVN Support** — The application shall support SVN repositories directly.

Required SVN features: detect working copy, show local modifications, compare working copy against base, compare revisions, compare branches/tags by URL, show file history, parse SVN diff output, open conflicted files in 3-way mode where metadata allows, create patch from selected changes.

Implementation may call SVN CLI initially.

**FR-032: VCS Abstraction Layer** — The application shall implement a VCS provider abstraction.

Initial providers: Git, SVN, local filesystem.

Future providers: Mercurial, Perforce, Fossil, Azure DevOps server-side diff import, GitHub/GitLab pull request import.

**FR-033: External Tool Compatibility** — The application shall support launching from command line.

Examples:

```
shiftdiff old.cs new.cs
shiftdiff base.cs local.cs remote.cs
shiftdiff base.cs local.cs remote.cs resolved.cs
shiftdiff --patch changes.diff --source old.cs
shiftdiff --git commitA commitB
shiftdiff --svn -r 1200:1250 path/to/file
```

The application shall be usable as an external diff/merge tool for Git and SVN.

### 8.5 User Interface

**FR-040: Cross-Platform Native UI** — The application shall provide a polished desktop UI with native-feeling behavior.

Recommended UI stack candidates: Avalonia UI, Qt, Tauri with native backend, Electron only if performance remains acceptable.

Given the performance and desktop requirements, Avalonia or Qt are preferred.

**FR-041: Drag-and-Drop** — The UI shall support drag-and-drop for: files, folders, patch files, repository folders, multiple selected files.

Drop behavior shall be context-aware.

**FR-042: Pane Layouts** — The UI shall support: side-by-side two-way view, three-way merge view, four-way comparison view, unified view, compact overview/minimap, focus mode for one selected block.

**FR-043: Visual Language** — The UI shall use clear symbols and restrained emojis.

Suggested markers:

- ✅ unchanged
- ✏️ edited
- 🚚 moved
- 🔀 moved + edited
- ➕ added
- ➖ removed
- ⚠️ conflict or uncertain match
- 🧩 split/merged block
- 🔒 read-only file
- 🧪 generated/reconstructed target
- 🕘 historical revision
- 🌿 branch
- 🧷 patch hunk

Emoji usage shall be optional and disableable. The UI must not rely on emoji alone — every emoji marker must also have text, tooltip, shape, or color semantics.

**FR-044: Accessibility** — The UI shall support: keyboard navigation, screen reader labels, high contrast mode, colorblind-safe themes, scalable font size, reduced animation mode, emoji-free mode.

**FR-045: Navigation** — The UI shall provide: next/previous change, next/previous conflict, next/previous moved block, jump to paired moved block, breadcrumb navigation for structured files, file list sidebar, search within diff, filter by change type.

**FR-046: Change Details Panel** — Selecting a block shall show: change type, confidence, source location, target location, similarity score, reason for match, affected lines, detected move path, patch hunk origin if applicable.

Advanced users shall be able to inspect why the block was matched.

**FR-047: Inline Editing** — The application shall optionally allow editing the constructed target file.

Required for merge mode: accept left, accept right, accept both, accept neither, edit manually, reset block, mark resolved.

Editing original files directly shall require explicit confirmation.

### 8.6 Performance

**FR-050: Large File Performance** — Targets: 10,000 lines interactive under 200 ms after load; 100,000 lines initial diff under 2 seconds where feasible; 1,000,000 lines degraded mode with progressive loading.

**FR-051: Progressive Analysis** — The application shall display basic diff results quickly, then refine semantic detection in the background.

Stages: 1) load files, 2) basic line diff, 3) anchor detection, 4) moved block detection, 5) token-level diff, 6) semantic refinement.

The UI shall remain responsive during analysis.

**FR-052: Cancellation** — The user shall be able to cancel long-running analysis.

**FR-053: Caching** — The application shall cache: file hashes, line hashes, token fingerprints, parsed structure trees, VCS revision content where safe.

Cache invalidation shall use file path, size, timestamp, content hash, and revision ID.

**FR-054: Memory Usage** — The application shall avoid loading unnecessary duplicate copies of large files.

Preferred strategies: memory-mapped files for large inputs, rope/piece-table representation for editable target files, pooled buffers, streaming patch parsing, compact line index tables, incremental rendering.

### 8.7 File Type Awareness

**FR-060: Generic Text** — All text files shall support line and token diffing.

**FR-061: Source Code** — The application should support language-aware tokenization for common languages.

Initial set: C#, JavaScript / TypeScript, Java, C / C++, Python, Go, Rust, PHP, HTML, CSS, SQL.

Language-specific parsing is optional for MVP but tokenization should be extensible.

**FR-062: Structured Data** — The application should support structure-aware comparison for: JSON, YAML, XML, TOML, INI, ".csproj", ".sln", package manifests.

For structured data, object key reordering should not always be treated as meaningful change.

**FR-063: Markdown** — Markdown comparison should understand: headings, paragraphs, lists, code fences, tables.

Moved sections under headings should be detected as blocks.

## 9. Algorithmic Requirements

### 9.1 Initial Diff Pipeline

The MVP algorithm shall use the following pipeline:

1. Load and normalize files.
2. Split into lines.
3. Compute multiple hashes per line.
4. Build hash indexes.
5. Discard or downrank common hashes.
6. Create anchor candidates.
7. Cluster anchors by diagonal offset.
8. Grow blocks from anchor clusters.
9. Score candidate block matches.
10. Resolve competing matches.
11. Classify changes.
12. Run internal diff inside matched blocks.
13. Render results.

### 9.2 Matching Strategy

The matcher shall prefer:

1. exact unique line anchors
2. normalized unique line anchors
3. token-normalized anchors
4. shingle-based block similarity
5. context-based fallback

The matcher shall penalize: huge block size mismatch, extremely common lines, reordered anchors inside a supposed block, weak token overlap, too many skipped lines, many-to-many ambiguity.

### 9.3 Block Growth Rules

Block growth shall use bounded lookahead.

Configuration defaults:

- minimum moved block size: 3 meaningful lines
- default lookahead window: 8 lines
- maximum weak gap: 4 lines
- maximum size ratio: 0.5 to 2.0
- pure move threshold: 0.90
- moved + edited threshold: 0.65
- uncertain threshold: 0.50
- below uncertain threshold: no semantic match

These are starting values and must be empirically tuned.

### 9.4 Duplicate Handling

The algorithm shall detect low-information anchors.

Examples of weak anchors: blank lines, braces only, punctuation-only lines, import separators, comment delimiters, repeated license headers, repeated XML closing tags.

Weak anchors may support a match but must not create one alone.

### 9.5 Split and Merge Detection

The algorithm should detect: one old block split into multiple new blocks; multiple old blocks merged into one new block.

Initial implementation may support constrained cases: one-to-two, two-to-one, adjacent blocks only, high combined similarity required.

Unconstrained many-to-many matching is not required for MVP.

## 10. UX Requirements

### 10.1 Main Screen

The main screen shall include: file/repository selector, drag-and-drop zone, recent sessions, quick actions (compare files, compare folders, open repository, apply patch, resolve conflict).

### 10.2 Diff Screen

The diff screen shall include: file tree / changed files sidebar, comparison panes, minimap, change navigation toolbar, change details inspector, status bar, semantic mode selector.

Semantic mode selector: Strict, Balanced, Aggressive.

### 10.3 Four-File UI

Four-file comparison shall avoid visual chaos.

Required features: collapse/hide individual panes, choose primary target pane, pin panes, synchronized scrolling, independent scrolling toggle, focus block mode, per-pane labels, color/marker consistency.

### 10.4 Conflict Resolution UI

For conflicts, user shall be able to: accept version A, accept version B, accept version C where applicable, accept reconstructed result, manually edit, mark resolved, export result.

## 11. Configuration

The user shall be able to configure: ignored whitespace, ignored casing, ignored comments, line ending behavior, encoding fallback, semantic aggressiveness, minimum moved block size, moved block threshold, UI theme, emoji markers on/off, color scheme, external Git/SVN executable paths, cache location, maximum memory usage.

## 12. Command-Line Interface

The application shall provide a CLI usable for automation.

Required commands:

```
shiftdiff compare <old> <new>
shiftdiff compare3 <base> <left> <right>
shiftdiff compare4 <base> <left> <right> <target>
shiftdiff apply-patch <source> <patch> --out <target>
shiftdiff export-patch <old> <new> --out <patch>
shiftdiff git diff [args]
shiftdiff svn diff [args]
```

CLI output formats: human-readable text, JSON, exit codes for CI usage.

Suggested exit codes:

- 0: no differences
- 1: differences found
- 2: conflicts or uncertain patch application
- 3: invalid input
- 4: internal error

## 13. Architecture

### 13.1 Major Components

**Diff Core** — normalization, hashing, anchor detection, block matching, line diffing, token diffing, semantic classification.

**Patch Engine** — parsing patches, applying hunks, fuzzy matching, target construction, patch export.

**VCS Layer** — Git integration, SVN integration, repository detection, revision content retrieval, changed file listing.

**Document Model** — file representation, line tables, block tables, change graph, pane synchronization, editable target representation.

**UI Layer** — rendering panes, navigation, drag-and-drop, merge controls, settings, user interaction.

**Plugin Layer** — language tokenizers, structured parsers, VCS providers, export formats.

### 13.2 Suggested Technology Stack

Preferred implementation:

- Core engine: C# / .NET 9
- UI: Avalonia UI
- CLI: .NET console application
- Git integration: Git CLI first, libgit2 optional later
- SVN integration: SVN CLI first
- Storage/cache: local filesystem, SQLite optional later

Reasoning:

- .NET 9 gives high performance and cross-platform deployment.
- Avalonia provides cross-platform desktop UI without dragging a browser engine everywhere like a dead whale.
- A shared C# core allows the GUI and CLI to use the same diff engine.

## 14. Data Model

### 14.1 Diff Session

A diff session contains: session ID, input files, file roles, normalization settings, VCS metadata, patch metadata, computed changes, user decisions, reconstructed output.

### 14.2 File Role

Supported roles: base, old, new, left, right, target, reconstructed, patch source, patch result.

### 14.3 Block Match

A block match contains: old file ID, new file ID, old start line, old end line, new start line, new end line, match type, confidence, similarity score, anchor count, weak anchor count, mismatch count, gap count, internal changes.

### 14.4 Change Type

Required change types: unchanged, edited, added, removed, moved, moved edited, split, merged, conflict, uncertain.

## 15. Security and Privacy

**SEC-001: Local-First** — The application shall process files locally by default. No source code, file contents, diffs, repository data, or metadata shall be uploaded without explicit user action.

**SEC-002: Patch Safety** — Patch application shall never overwrite files without confirmation. The application shall default to writing reconstructed files to a new path.

**SEC-003: Repository Safety** — VCS operations shall avoid destructive commands unless explicitly requested. Initial release shall not perform commits, pushes, rebases, resets, or checkouts.

**SEC-004: Large File Protection** — The application shall warn before opening extremely large files or folders.

## 16. Performance Benchmarks

**Small** — 1,000 lines, simple edits, one moved block. Expected: diff under 100 ms.

**Medium** — 25,000 lines, several moved blocks, 5–10% edits. Expected: initial result under 1 second, semantic refinement under 3 seconds.

**Large** — 250,000 lines, generated file, repeated blocks. Expected: responsive UI, progressive rendering, no crash, degraded mode acceptable.

**Pathological** — many repeated identical blocks, huge JSON, minified JS, massive XML, files with mixed encodings. Expected: no infinite matching, no catastrophic memory growth, clear degraded-mode message.

## 17. MVP Scope

### 17.1 MVP Must Have

- Cross-platform desktop app
- Two-way file diff
- Three-way file diff
- Four-file viewing layout
- Drag-and-drop files
- Basic folder comparison
- Line hash based moved block detection
- Moved + edited block classification
- Inline line/token diff inside matched blocks
- Unified diff parsing
- Target file reconstruction from source + patch
- Git working tree diff support
- SVN working copy diff support
- CLI compare command
- Light and dark themes
- Emoji markers with disable option

### 17.2 MVP Should Have

- Git commit/branch comparison
- SVN revision comparison
- fuzzy patch application
- structured JSON comparison
- Markdown section matching
- conflict resolution for three-way merge
- exported patch generation

### 17.3 MVP Could Have

- language-aware C# tokenizer
- minimap
- session save/load
- custom ignore rules
- syntax highlighting
- plugin API draft

### 17.4 Not MVP

- full AST diff
- semantic refactoring detection
- AI merge recommendations
- remote pull request review
- cloud sync
- collaborative editing

## 18. Future Versions

**Version 1.1** — better Git integration, stage/unstage selected hunks, improved SVN revision browser, structured JSON/YAML/XML diff, C#/JS/TS/Python tokenizers, saved sessions, configurable keyboard shortcuts.

**Version 1.2** — AST-assisted block matching, plugin SDK, Mercurial support, Perforce support, richer merge editor, visual rename/copy tracking.

**Version 2.0** — pull request import, review comments, semantic refactoring hints, batch repository comparison, CI report generation, team configuration profiles.

## 19. Acceptance Criteria

**AC-001: Moved Block Detection** — Given two files where a block of at least 10 meaningful lines was moved and 20% of its lines were edited, the application shall classify it as moved + edited in balanced mode with at least likely confidence.

**AC-002: Duplicate Noise Resistance** — Given files containing many repeated braces, blank lines, and import statements, the application shall not create moved block matches based only on those lines.

**AC-003: Four-File Comparison** — Given four files, the application shall display all four, synchronize navigation, and allow the user to hide/show panes without restarting the comparison.

**AC-004: Patch Reconstruction** — Given a source file and compatible unified diff, the application shall reconstruct the target file exactly.

**AC-005: Fuzzy Patch Reconstruction** — Given a source file where patch context moved by line offset but is otherwise recognizable, the application shall apply the patch in fuzzy mode and mark the result as high confidence.

**AC-006: Git Integration** — Given a Git repository, the application shall detect local changes and open selected changed files in the semantic diff viewer.

**AC-007: SVN Integration** — Given an SVN working copy, the application shall detect local modifications and open selected changed files in the semantic diff viewer.

**AC-008: Drag-and-Drop** — Given two, three, or four dropped files, the application shall open the corresponding comparison mode automatically.

**AC-009: Responsiveness** — The UI shall remain responsive while analyzing a 100,000-line file pair.

**AC-010: Export Safety** — The application shall not overwrite any existing file during reconstruction unless the user explicitly confirms the overwrite.

## 20. Risks

**R-001: False Move Detection** — Aggressive matching may incorrectly classify unrelated blocks as moved. Mitigation: confidence scoring, strict default thresholds, weak anchor suppression, user-visible explanation, semantic mode selector.

**R-002: Performance Collapse on Repeated Content** — Generated files or boilerplate may produce too many candidate matches. Mitigation: frequency caps, candidate limits, rarity weighting, progressive degradation.

**R-003: Four-Way UI Complexity** — Four panes can become unreadable. Mitigation: collapsible panes, focus mode, block inspector, minimap, clear role labels.

**R-004: Patch Reconstruction Ambiguity** — Fuzzy patch application can produce wrong output. Mitigation: confidence levels, candidate selection UI, never overwrite by default, explicit uncertain hunk review.

**R-005: VCS Backend Differences** — Git and SVN behave differently and expose different metadata. Mitigation: VCS abstraction, provider-specific capabilities, CLI backend first, normalized internal change model.

## 21. Open Questions

- Should four-way comparison support true four-way merge logic, or only visual comparison plus target reconstruction?
- Should syntax highlighting be built in or delegated to a library?
- Should the first release support editing source panes, or only target panes?
- How much SVN metadata should be cached?
- Should repository integrations perform write actions such as staging in v1, or remain read-only initially?
- Should plugin support be public in v1, or internal only until APIs stabilize?

## 22. Success Metrics

ShiftDiff is successful if:

- users can review large refactor diffs without mentally reconstructing moved blocks
- moved + edited blocks are detected reliably enough to be useful
- patch reconstruction works for normal Git/SVN diffs
- four-file comparison is usable rather than ornamental
- performance remains interactive on real repositories
- users trust the confidence system
- users do not immediately disable semantic mode out of self-defense

## 23. Summary

ShiftDiff shall be a fast, cross-platform semantic diff viewer that treats file comparison as a relationship problem, not just a line-number problem.

Its first major differentiator is reliable detection of moved and edited blocks using normalized line hashes, anchor clustering, fuzzy block growth, and confidence scoring.

Its second major differentiator is support for up to four files at once, enabling real merge, review, generated-output, and patch-reconstruction workflows.

Its third major differentiator is direct Git and SVN support with local-first behavior and a clean UI that explains what changed, where it moved, and how confident the tool is.

The result should feel like a serious developer tool: fast, explainable, safe, and sharp enough to make ordinary diff viewers look mildly embarrassed.
