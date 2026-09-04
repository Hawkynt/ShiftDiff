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

## Sourcing an implementation

Never write a format, codec, cipher or compression scheme out of your own understanding when
somebody has already got it right. Work **down** this ladder, stop at the first rung that applies,
and say in the commit body which rung you used and why the ones above it did not.

**1 — Licence-compatible source you can take.** MIT, BSD, Apache-2.0, LGPL, public domain: anything
this repository's LGPL-3.0-or-later can absorb. Search for it before writing anything. There are two
ways to take it and the choice is not cosmetic:

- **Vendor it** — a verbatim subtree under `Vendored/<Library>/` next to its own `LICENSE.txt`, kept
  in the upstream's own formatting. Do *not* restyle it: the whole point is that the next upstream
  version still applies cleanly, and a reformatted copy conflicts on every update. Keep it out of
  the published API surface with the `exclude-namespace` input of the `package-readme` action rather
  than by editing the source.
- **Convert it** — carry the algorithm across into this codebase properly. Converted code is *our*
  code, so every rule this guide sets for our own code applies to it, including the current C#
  language version (C# 14) wherever that says the same thing more plainly. Do not restate those
  rules here or anywhere else: one stale copy of them is how this guide spent years asking for a
  brace style the code had never used. A conversion that still reads like C, or like a decompiler's
  output, is not finished.

Either way, record where it came from — a `THIRD_PARTY_NOTICES.md` in the package, or a
`THIRD-PARTY-NOTICE.<Name>.txt` beside the code. Attribution is a licence term, not a courtesy.

**2 — Licence-incompatible source: use it, but not its code.** GPL where we ship LGPL, anything
proprietary, anything with no licence at all. Read it and *build material from it*: a written
specification, a set of test cases, and a third-party oracle you can run to produce expected output.
Then implement from that derived material. Do not paste it, do not transliterate it line by line,
and do not carry its file layout or its identifier names across — that is still the same copy.

**Constants are not expression.** Tables, S-boxes, magic numbers, CRC polynomials, Huffman code
tables, quantisation matrices, window and filter coefficients: copy them exactly, from whichever
source is authoritative, on every rung of this ladder. A re-derived S-box is simply a wrong S-box,
and a table somebody worked out for themselves is the defect that nothing catches until real files
arrive. Where a value is arbitrary-but-agreed, matching it *is* the specification.

**3 — Original reference material.** The specification, the standard (RFC, ITU-T, ISO, ECMA), the
academic paper, the vendor's own documentation, the format author's write-up. Prefer the normative
text over anybody's description of it; where the two disagree, the normative text wins and the
disagreement is worth a comment.

**4 — Other trusted sources.** Reverse-engineering write-ups, articles and blog posts by named
people with a track record, and long-lived project wikis that cite their evidence.

**5 — Untrusted material, by agreement only.** Forum answers, unattributed gists, wiki edits with no
provenance. Only when nothing above exists, and only where several *independent* sources agree —
majority vote, discounting the ones that plainly copied each other. Treat the result as a hypothesis
and mark it as one in the code.

Whatever rung you land on, the finished implementation is judged the same way: it must agree with an
oracle or with real files, not merely compile and look plausible. When a licence-incompatible
implementation was your oracle, keep the comparison as a test wherever it can run, and where it
cannot, commit the captured expected output with a note saying what produced it.

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
