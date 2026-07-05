# CI/CD Pipeline — ShiftDiff

> Everything in this folder is the automated pipeline for this repository.
> Workflows live here, their helper scripts live in `scripts/`.
>
> This is the shared Hawkynt-repo-family template, adapted for ShiftDiff.

## What this does

Three workflows, one shared build block, three helper scripts:

| File                            | Trigger                             | Purpose                                   |
|---------------------------------|--------------------------------------|-------------------------------------------|
| `ci.yml`                        | push + PR + `workflow_call`         | Build + test on ubuntu/windows            |
| `release.yml`                   | **manual dispatch**                 | Package + publish, then tag `vyyyyMMdd`   |
| `nightly.yml`                   | successful CI run on `main`         | Publish `nightly-yyyyMMdd` prerelease     |
| `_build.yml`                    | `workflow_call` (internal)          | Publishes the `ShiftDiff.Cli` binaries    |
| `scripts/version.pl`            | invoked by the workflows            | Stamp each csproj's own `<Version>` + build |
| `scripts/update-changelog.mjs`  | invoked by the workflows            | Bucketise commits into CHANGELOG.md       |
| `scripts/prune-nightlies.mjs`   | invoked by the workflows            | 3-gen (GFS) retention of nightlies        |

## How it works

```
                push / PR
                    │
                    ▼
            ┌───────────────┐
            │    ci.yml     │──► dotnet test on ubuntu + windows
            └───┬───────┬───┘
                │       │
   dispatch ────┤       │  on success on main (default branch)
                ▼       ▼
        ┌──────────┐  ┌─────────────┐
        │ release  │  │  nightly    │
        │  .yml    │  │   .yml      │
        └────┬─────┘  └─────┬───────┘
             │              │
             ▼              ▼
        (both call _build.yml — publishes ShiftDiff.Cli win-x64 + linux-x64)
             │              │
             ▼              ▼
  publish + tag vyyyyMMdd  nightly-yyyyMMdd (prerelease)
                                │
                                ▼
                       scripts/prune-nightlies.mjs
                       (GFS: 7 daily + 4 weekly + 3 monthly)
```

## What it's for

- Every PR is built and tested on ubuntu + windows before it can merge.
- Every merge to `main` produces a **tested** nightly prerelease.
- A **manual dispatch** cuts a stable release from artifacts built by `_build.yml`, then tags the dated `vyyyyMMdd` Release at that commit.
- Old nightlies are auto-pruned on a **Grandfather-Father-Son** schedule.

## Why it's built this way

- **No cron triggers.** Event-driven only — CI fires on PRs, nightlies fire when CI passes on main, stable releases fire on manual dispatch.
- **Files drive versions, per-package, never tags.** `version.pl --stamp` appends the commit count to each csproj's own `<Version>`. There is no single repo version, so the repo-level Release/tag is the date marker `vyyyyMMdd`.
- **Release calls CI via `workflow_call`.** Calling ci.yml explicitly keeps tests and releases in lockstep with zero copy-paste.
- **Nightly builds from the `workflow_run` payload's SHA**, not branch tip — so a nightly is always a build of code CI actually validated.
- **`_build.yml` is the single packaging block**, shared by release and nightly so they never diverge. It runs on windows-latest so one host can publish both win-x64 and linux-x64 self-contained binaries without cross-runner artifact passing.
- **3-generation (GFS) retention**, not "keep last N". GFS guarantees at least one build per week for a month and one per month for a quarter.

## Scripts

### `version.pl`

Each package's version is derived from the **nearest declaration** — currently
`src/ShiftDiff.Core/ShiftDiff.Core.csproj`, `src/ShiftDiff.Cli/ShiftDiff.Cli.csproj`
and the test project each carry their own `<Version>`. BUILD = commit count of
the declaring file's parent folder.

```
perl .github/workflows/scripts/version.pl --stamp  # rewrite the version in every DECLARING file
perl .github/workflows/scripts/version.pl --build  # print the repo-wide build number (commit count)
perl .github/workflows/scripts/version.pl --list   # "<file>\t<effective-version>" per package
```

> There is no single repo version. Stable releases are tagged with a **date
> marker** `vyyyyMMdd`, not a version.

### `update-changelog.mjs`

Prepends a new section to `CHANGELOG.md` and/or writes release-notes bodies (`--notes <file>`). Commit-subject convention: `+` Added, `*` Changed, `#` Fixed, `-` Removed, `!` TODO, anything else → Other.

- **Releases** measure from the last **stable** tag (`v[0-9]*`) → a release's notes contain *everything since the last release*.
- **Nightlies** measure from the nearest tag of any kind → a nightly's notes contain *only the delta since the previous nightly*. `nightly.yml` passes `--notes-only` so `CHANGELOG.md` is only ever committed by `release.yml`.

### `prune-nightlies.mjs`

GFS retention with `DAILY_KEEP=7`, `WEEKLY_KEEP=4`, `MONTHLY_KEEP=3`. Dry-run with `--dry-run`.

## Who maintains this

This is the shared template for the Hawkynt repo family. When changing it,
prototype in the template then mirror the change to the consuming repos.

## Release artifacts

| Artifact                                 | Produced by          |
|------------------------------------------|-----------------------|
| `app-artifacts` (win-x64 + linux-x64)    | release + nightly     |
