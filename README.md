# ShiftDiff

Semantic diff viewer for moved, edited, merged, and reconstructed content.

Compares up to four files at once, detects moved/edited blocks (not just
delete+add), reconstructs target files from patches, and integrates directly
with Git and SVN.

Full spec: [SPEC.md](SPEC.md).

## Layout

- `src/ShiftDiff.Core` — diff engine (hashing, anchor detection, block
  matching, patch parsing/application). No UI or VCS dependencies.
- `src/ShiftDiff.Cli` — command-line entry point.
- `tests/ShiftDiff.Core.Tests` — xunit tests for the core engine.

UI project (Avalonia) and VCS-integration project are added once the core
engine covers enough of section 8.2/8.3 of the spec to be worth wrapping.

## Building

```
dotnet build
dotnet test
```

## Workflow

TDD/BDD/DDD/SDD: every feature starts from a spec requirement (FR-xxx/AC-xxx
in SPEC.md), gets a failing test named after the behavior it pins, then the
implementation that turns it green. Domain terms in code match the spec's
vocabulary (block, anchor, hunk, confidence, role, etc.).
