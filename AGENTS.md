# Working on ShiftDiff

Full product spec: `SPEC.md`. Every change should trace back to a
requirement or acceptance criterion in there (FR-xxx / AC-xxx / SEC-xxx).

## Methodology — TDD / BDD / DDD / SDD, always

- **Spec-Driven:** pick the smallest unimplemented requirement from
  `SPEC.md`, implement only that.
- **Test-Driven:** write the failing test first, then the code that makes
  it pass. Bug fixes start with a regression test that reproduces the bug.
- **Behavior-Driven:** name and structure tests around observable behavior
  (given/when/then), not implementation details.
- **Domain-Driven:** use the spec's vocabulary in code — block, anchor,
  hunk, confidence, role, session, etc. (section 14, Data Model).
- Flow: test (red) -> implementation (green) -> refactor.

## No AI traces — ever

Never mention an AI assistant, model, or tool in code, comments, commit
messages, or any committed artifact. No "Generated with...", no
"Co-Authored-By" attribution lines. Committed output must be
indistinguishable from a human developer's.

## Commit message format

Multiline allowed. Each line starts with one marker:

- `+` added feature / new capability
- `-` removed behavior or public API
- `*` changed behavior or API
- `#` bug fix
- `!` important TODO — something still broken / needs follow-up

One change per line; group related lines in a single message. Describe the
change only, no tooling/authorship attribution.

## Layout

- `src/ShiftDiff.Core` — diff engine, no UI/VCS dependencies.
- `src/ShiftDiff.Vcs` — Git/SVN providers behind `IVcsProvider`; all process
  calls go through `IProcessRunner` so they stay testable.
- `src/ShiftDiff.Ui` — presentation layer (document model, navigation, merge).
  No UI framework dependency: put logic here, not in the window.
- `src/ShiftDiff.Cli` — command-line entry point.
- `src/ShiftDiff.App` — Avalonia shell; keep it thin.
- `tests/<project>.Tests` — xunit per project. `ShiftDiff.App.Tests` runs
  headless Avalonia (xunit v3) and can render frames to `SHIFTDIFF_SHOTS`.

Run `dotnet test` before every commit.
