using System.Diagnostics;
using System.Text;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class PerformanceTests
{
    // Realistic FR-050 scenario: a 100,000-line file with a small clustered
    // edit region (e.g. one function changed), everything else untouched.
    // Common prefix/suffix trimming keeps LineDiffer's own LCS table tiny
    // here, but FileComparer.Compare still runs full-file block/move
    // detection (BlockBuilder/BlockClassifier) over all 100,000 lines on
    // both sides regardless of how much is unchanged. Two redundancy fixes
    // (merging AnchorDetector's dual hash pass, then replacing every
    // single-tier LineHasher.Hash(line).Tier call with a HashRaw/
    // HashWhitespaceNormalized call that skips the other 3 SHA-256 tiers)
    // brought this down from ~6.9s to ~1-2s (Debug ~2s, Release ~1s) —
    // close to FR-050's 2s target but not a formal compliance claim (Debug
    // build, single environment). Remaining known redundancy, not yet
    // attempted: BlockSimilarityScorer's per-candidate scoring functions
    // (ExactHashOverlap/NormalizedHashOverlap/HashRange/
    // NeighboringBlockConsistency) each rehash the same lines independently
    // per candidate rather than sharing anchors' already-computed hashes.
    // This test is a regression guard against a *worse* crash-or-hang, not
    // a claim of FR-050 compliance — don't tighten the threshold to 2000ms
    // until that follow-up is measured too.
    [Fact]
    public void Compare_100000LineFilesWithClusteredEdit_CompletesWithoutCrashingOrHanging()
    {
        var oldLines = GenerateLines(100_000, seed: 42);
        var newLines = ApplyClusteredEdit(oldLines, editStart: 50_000, editLength: 200);

        var oldContent = Encoding.UTF8.GetBytes(string.Join('\n', oldLines) + "\n");
        var newContent = Encoding.UTF8.GetBytes(string.Join('\n', newLines) + "\n");

        var stopwatch = Stopwatch.StartNew();
        var result = FileComparer.Compare(oldContent, newContent);
        stopwatch.Stop();

        Assert.Contains(result.Changes, change => change.ChangeType == ChangeType.Edited);
        Assert.True(stopwatch.ElapsedMilliseconds < 15_000,
            $"regression guard, not FR-050 compliance (still open): took {stopwatch.ElapsedMilliseconds}ms");
    }

    // Adversarial case: edits scattered across the whole file defeat prefix/
    // suffix trimming (there's no untouched common region left), so the exact
    // LCS table would need to cover ~all 100,000x100,000 cells — infeasible to
    // allocate. FR-050's degraded mode (UniqueCommonLineSynchronizer) handles
    // this: every unedited line here is already globally unique (it embeds its
    // own line number), so the whole file synchronizes on those lines and only
    // the tiny gaps around each edit need the exact DP table.
    [Fact]
    public void Compare_100000LineFilesWithScatteredEdits_DegradesInsteadOfThrowing()
    {
        var oldLines = GenerateLines(100_000, seed: 42);
        var newLines = ApplyScatteredEdits(oldLines, editFraction: 0.05, seed: 43);

        var oldContent = Encoding.UTF8.GetBytes(string.Join('\n', oldLines) + "\n");
        var newContent = Encoding.UTF8.GetBytes(string.Join('\n', newLines) + "\n");

        var stopwatch = Stopwatch.StartNew();
        var result = FileComparer.Compare(oldContent, newContent);
        stopwatch.Stop();

        Assert.Contains(result.Changes, change => change.ChangeType == ChangeType.Edited);
        Assert.True(stopwatch.ElapsedMilliseconds < 15_000,
            $"degraded-mode regression guard, not FR-050 compliance (still open): took {stopwatch.ElapsedMilliseconds}ms");
    }

    // True worst case: no line is shared between old and new at all, so there is
    // nothing to synchronize on. The coarsest valid result (whole file replaced)
    // must still come back instead of an OutOfMemoryException / crash.
    [Fact]
    public void Compare_100000LineFilesWithNoSharedLines_ReplacesWholeFileInsteadOfCrashing()
    {
        // Distinct per-side prefixes guarantee zero exact-line overlap by
        // construction, rather than relying on random content happening not to collide.
        var oldLines = GenerateLines(100_000, seed: 42).Select(line => "OLD-" + line).ToArray();
        var newLines = GenerateLines(100_000, seed: 99).Select(line => "NEW-" + line).ToArray();

        var oldContent = Encoding.UTF8.GetBytes(string.Join('\n', oldLines) + "\n");
        var newContent = Encoding.UTF8.GetBytes(string.Join('\n', newLines) + "\n");

        var result = FileComparer.Compare(oldContent, newContent);

        // Equal-length Removed/Added runs are coalesced into Edited substitution
        // pairs by CoalesceAdjacentRemovedAndAddedIntoEdited — this is the existing,
        // correct representation for a full-file replacement, not Removed+Added.
        Assert.All(result.Changes, change => Assert.Equal(ChangeType.Edited, change.ChangeType));
        Assert.Equal(100_000, result.Changes.Count());
    }

    private static string[] GenerateLines(int count, int seed)
    {
        var random = new Random(seed);
        var lines = new string[count];
        for (var i = 0; i < count; i++)
        {
            lines[i] = $"line {i} token{random.Next(1000)} payload{random.Next(100_000)}";
        }
        return lines;
    }

    private static string[] ApplyClusteredEdit(string[] original, int editStart, int editLength)
    {
        var lines = (string[])original.Clone();
        for (var i = editStart; i < editStart + editLength && i < lines.Length; i++)
        {
            lines[i] = lines[i] + " EDITED";
        }
        return lines;
    }

    private static string[] ApplyScatteredEdits(string[] original, double editFraction, int seed)
    {
        var random = new Random(seed);
        var lines = (string[])original.Clone();
        var editCount = (int)(lines.Length * editFraction);
        for (var i = 0; i < editCount; i++)
        {
            var index = random.Next(lines.Length);
            lines[index] = lines[index] + " EDITED";
        }
        return lines;
    }
}
