using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class PatchApplierTests
{
    [Fact]
    public void AllContextHunk_NoActualChange_OutputEqualsInput()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Context, "b"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var result = PatchApplier.ApplyHunkExact(source, hunk);

        Assert.Equal(source, result);
    }

    [Fact]
    public void SingleHunk_ReplacingOneLine_RemovedAddedPair()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var result = PatchApplier.ApplyHunkExact(source, hunk);

        Assert.Equal(new[] { "a", "B", "c" }, result);
    }

    [Fact]
    public void HunkAtVeryStart_OldStartOne_AppliesFromBeginning()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 1, 1, 1),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Removed, "a"),
                new(UnifiedDiffLineKind.Added, "A"),
            });

        var result = PatchApplier.ApplyHunkExact(source, hunk);

        Assert.Equal(new[] { "A", "b", "c" }, result);
    }

    [Fact]
    public void HunkAtVeryEnd_CoversLastLine_AppliesThroughEnd()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(3, 1, 3, 1),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Removed, "c"),
                new(UnifiedDiffLineKind.Added, "C"),
            });

        var result = PatchApplier.ApplyHunkExact(source, hunk);

        Assert.Equal(new[] { "a", "b", "C" }, result);
    }

    [Fact]
    public void ContextMismatch_ThrowsPatchApplicationException()
    {
        var source = new[] { "a", "x", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        Assert.Throws<PatchApplicationException>(() => PatchApplier.ApplyHunkExact(source, hunk));
    }

    [Fact]
    public void MultiHunkFile_AppliesEachHunkInOrder()
    {
        var source = new[] { "a", "b", "c", "d", "e" };
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("a/f", "b/f"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 1, 1, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "a"),
                        new(UnifiedDiffLineKind.Added, "A"),
                    }),
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(5, 1, 5, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "e"),
                        new(UnifiedDiffLineKind.Added, "E"),
                    }),
            });

        var result = PatchApplier.ApplyFileExact(source, file);

        Assert.Equal(new[] { "A", "b", "c", "d", "E" }, result);
    }

    [Fact]
    public void Fuzzy_HunkAtItsRecordedPosition_ReturnsExactConfidence()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var result = PatchApplier.ApplyHunkFuzzy(source, hunk);

        Assert.Equal(new[] { "a", "B", "c" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.Exact, result.Confidence);
    }

    [Fact]
    public void Fuzzy_ContextShiftedByLineOffset_FindsItAndReturnsHighConfidence()
    {
        // Hunk header still claims the old block starts at line 1, but two
        // unrelated lines were prepended to the source, shifting the real
        // context down to line 3 — this is AC-005's "line offset drift".
        var source = new[] { "x", "y", "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var result = PatchApplier.ApplyHunkFuzzy(source, hunk);

        Assert.Equal(new[] { "x", "y", "a", "B", "c" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.High, result.Confidence);
    }

    [Fact]
    public void Fuzzy_ContextNotFoundAnywhereInSource_ThrowsPatchApplicationException()
    {
        var source = new[] { "x", "y", "z" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        Assert.Throws<PatchApplicationException>(() => PatchApplier.ApplyHunkFuzzy(source, hunk));
    }

    [Fact]
    public void Fuzzy_PureInsertionHunkWithNoOldLines_AppliesAtRecordedPositionAsExact()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(2, 0, 2, 1),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Added, "NEW"),
            });

        var result = PatchApplier.ApplyHunkFuzzy(source, hunk);

        Assert.Equal(new[] { "a", "NEW", "b", "c" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.Exact, result.Confidence);
    }

    [Fact]
    public void FindFuzzyCandidates_BlockDuplicatedAtTwoLocations_ReturnsTwoExactCandidates()
    {
        // Counterpart to FindSemanticCandidates_BlockDuplicatedAtTwoLocations,
        // proving fuzzy mode *can* surface the ambiguous-duplicate case that
        // semantic mode structurally cannot (no anchor-uniqueness gate here).
        var source = new[] { "a", "b", "c", "x", "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var candidates = PatchApplier.FindFuzzyCandidates(source, hunk);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(1, candidates[0].LineNumber);
        Assert.Equal(PatchApplicationConfidence.Exact, candidates[0].Confidence);
        Assert.Equal(5, candidates[1].LineNumber);
        Assert.Equal(PatchApplicationConfidence.Exact, candidates[1].Confidence);
    }

    [Fact]
    public void FindFuzzyCandidates_UnambiguousMatch_ReturnsSingleCandidate()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var candidates = PatchApplier.FindFuzzyCandidates(source, hunk);

        Assert.Single(candidates);
        Assert.Equal(1, candidates[0].LineNumber);
        Assert.Equal(PatchApplicationConfidence.Exact, candidates[0].Confidence);
    }

    [Fact]
    public void FindFuzzyCandidates_ContextNotFoundAnywhereInSource_ReturnsEmpty()
    {
        var source = new[] { "x", "y", "z" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var candidates = PatchApplier.FindFuzzyCandidates(source, hunk);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindFuzzyCandidates_PureInsertionHunk_ReturnsEmpty()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(2, 0, 2, 1),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Added, "NEW"),
            });

        var candidates = PatchApplier.FindFuzzyCandidates(source, hunk);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindFuzzyCandidates_ExactAndDriftMatchAtDifferentPositions_OrdersExactFirstRegardlessOfDistance()
    {
        // Recorded position is index 0 (closest to the drift match), yet the
        // exact match at index 4 must still sort first — Exact always outranks
        // High confidence, distance only breaks ties within the same kind.
        var source = new[] { "a2", "b", "c", "x", "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var candidates = PatchApplier.FindFuzzyCandidates(source, hunk);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(PatchApplicationConfidence.Exact, candidates[0].Confidence);
        Assert.Equal(5, candidates[0].LineNumber);
        Assert.Equal(PatchApplicationConfidence.High, candidates[1].Confidence);
        Assert.Equal(1, candidates[1].LineNumber);
    }

    [Fact]
    public void ApplyFileExact_HunkSubsetSelectedViaWithExpression_OnlyAppliesSelectedHunk()
    {
        // FR-022 "selected diff changes": no dedicated API needed — a caller
        // filters UnifiedDiffFile.Hunks down to the desired subset via a
        // `with` expression before calling ApplyFileExact, same precedent as
        // FR-024's "selected changes only" export. Locks in the finding with
        // an actual regression test rather than leaving it an unverified note.
        var source = new[] { "a", "b", "c", "d" };
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("a/f", "b/f"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 1, 1, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "a"),
                        new(UnifiedDiffLineKind.Added, "A"),
                    }),
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(4, 1, 4, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "d"),
                        new(UnifiedDiffLineKind.Added, "D"),
                    }),
            });

        var selectedFile = file with { Hunks = new[] { file.Hunks[1] } };
        var result = PatchApplier.ApplyFileExact(source, selectedFile);

        Assert.Equal(new[] { "a", "b", "c", "D" }, result);
    }

    [Fact]
    public void ApplyPatchExact_MultipleFiles_EachRoutedToItsOwnSourceByPath()
    {
        var fileA = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("a/first.txt", "b/first.txt"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 1, 1, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "a"),
                        new(UnifiedDiffLineKind.Added, "A"),
                    }),
            });
        var fileB = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("a/second.txt", "b/second.txt"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 1, 1, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "x"),
                        new(UnifiedDiffLineKind.Added, "X"),
                    }),
            });
        var patch = new UnifiedDiffPatch(new[] { fileA, fileB });
        var sources = new Dictionary<string, IReadOnlyList<string>>
        {
            ["a/first.txt"] = new[] { "a", "b" },
            ["a/second.txt"] = new[] { "x", "y" },
        };

        var result = PatchApplier.ApplyPatchExact(patch, sources);

        Assert.Equal(new[] { "A", "b" }, result["b/first.txt"]);
        Assert.Equal(new[] { "X", "y" }, result["b/second.txt"]);
    }

    [Fact]
    public void ApplyPatchExact_RenamedFile_OutputKeyedByTargetPathNotSourcePath()
    {
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("a/old-name.txt", "b/new-name.txt"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 1, 1, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Context, "unchanged"),
                    }),
            });
        var patch = new UnifiedDiffPatch(new[] { file });
        var sources = new Dictionary<string, IReadOnlyList<string>>
        {
            ["a/old-name.txt"] = new[] { "unchanged" },
        };

        var result = PatchApplier.ApplyPatchExact(patch, sources);

        Assert.True(result.ContainsKey("b/new-name.txt"));
        Assert.False(result.ContainsKey("a/old-name.txt"));
    }

    [Fact]
    public void ApplyPatchExact_MissingSourceForAFile_ThrowsPatchApplicationException()
    {
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("a/missing.txt", "b/missing.txt"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 1, 1, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Context, "line"),
                    }),
            });
        var patch = new UnifiedDiffPatch(new[] { file });
        var sources = new Dictionary<string, IReadOnlyList<string>>();

        Assert.Throws<PatchApplicationException>(() => PatchApplier.ApplyPatchExact(patch, sources));
    }

    [Fact]
    public void Fuzzy_MultiHunkFile_OneHunkShiftedAndOneExact_FileConfidenceIsHighOverall()
    {
        // First hunk's context ("a") only occurs at its recorded exact
        // position. Second hunk's context ("e") was pushed one line down by
        // an unrelated insertion, so it can only be found via drift search.
        var source = new[] { "a", "b", "c", "d", "z", "e" };
        var file = new UnifiedDiffFile(
            new UnifiedDiffFileHeader("a/f", "b/f"),
            new[]
            {
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(1, 1, 1, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "a"),
                        new(UnifiedDiffLineKind.Added, "A"),
                    }),
                new UnifiedDiffHunk(
                    new UnifiedDiffHunkHeader(5, 1, 5, 1),
                    new UnifiedDiffLine[]
                    {
                        new(UnifiedDiffLineKind.Removed, "e"),
                        new(UnifiedDiffLineKind.Added, "E"),
                    }),
            });

        var result = PatchApplier.ApplyFileFuzzy(source, file);

        Assert.Equal(new[] { "A", "b", "c", "d", "z", "E" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.High, result.Confidence);
    }

    [Fact]
    public void Fuzzy_LeadingContextLineDrifted_AppliesAndPreservesSourceLeadingLine()
    {
        // "a" was edited to "a2" by an unrelated prior change; the Removed/
        // trailing-Context lines still match verbatim at the recorded position.
        var source = new[] { "a2", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var result = PatchApplier.ApplyHunkFuzzy(source, hunk);

        Assert.Equal(new[] { "a2", "B", "c" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.High, result.Confidence);
    }

    [Fact]
    public void Fuzzy_TrailingContextLineDrifted_AppliesAndPreservesSourceTrailingLine()
    {
        // "c" was edited to "c2" by an unrelated prior change; the leading
        // Context/Removed lines still match verbatim at the recorded position.
        var source = new[] { "a", "b", "c2" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        var result = PatchApplier.ApplyHunkFuzzy(source, hunk);

        Assert.Equal(new[] { "a", "B", "c2" }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.High, result.Confidence);
    }

    [Fact]
    public void Fuzzy_RemovedLineMismatch_StillThrowsEvenWithDriftTolerantContext()
    {
        // Context lines match fine, but the Removed line itself ("b") was
        // replaced by unrelated content ("x") — drift tolerance must never
        // extend to Removed lines, since those are the actual change.
        var source = new[] { "a", "x", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "a"),
                new(UnifiedDiffLineKind.Removed, "b"),
                new(UnifiedDiffLineKind.Added, "B"),
                new(UnifiedDiffLineKind.Context, "c"),
            });

        Assert.Throws<PatchApplicationException>(() => PatchApplier.ApplyHunkFuzzy(source, hunk));
    }

    [Fact]
    public void Semantic_TargetBlockMovedElsewhereInFile_AppliesViaBlockIdentityAndReturnsMovedConfidence()
    {
        // The hunk's recorded position (line 1) is nowhere near the block's
        // real location (index 5..7) — a position-based search anchored on
        // the recorded line would never look there. Semantic mode has to
        // recognize the block by its own content instead of by line number.
        var source = new[]
        {
            "filler line number one long enough content",
            "filler line number two long enough content",
            "filler line number three long enough content",
            "filler line number four long enough content",
            "filler line number five long enough content",
            "block target Alpha long enough content here",
            "block target Beta long enough content here",
            "block target Gamma long enough content here",
            "filler line number eight long enough content",
        };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "block target Alpha long enough content here"),
                new(UnifiedDiffLineKind.Removed, "block target Beta long enough content here"),
                new(UnifiedDiffLineKind.Added, "block target BETA long enough content here"),
                new(UnifiedDiffLineKind.Context, "block target Gamma long enough content here"),
            });

        var result = PatchApplier.ApplyHunkSemantic(source, hunk);

        Assert.Equal(new[]
        {
            "filler line number one long enough content",
            "filler line number two long enough content",
            "filler line number three long enough content",
            "filler line number four long enough content",
            "filler line number five long enough content",
            "block target Alpha long enough content here",
            "block target BETA long enough content here",
            "block target Gamma long enough content here",
            "filler line number eight long enough content",
        }, result.Lines);
        Assert.Equal(PatchApplicationConfidence.Moved, result.Confidence);
    }

    [Fact]
    public void Semantic_BlockContentAbsentEverywhereInFile_ThrowsPatchApplicationException()
    {
        var source = new[]
        {
            "totally unrelated line content number one",
            "totally unrelated line content number two",
            "totally unrelated line content number three",
        };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "block target Alpha long enough content here"),
                new(UnifiedDiffLineKind.Removed, "block target Beta long enough content here"),
                new(UnifiedDiffLineKind.Added, "block target BETA long enough content here"),
                new(UnifiedDiffLineKind.Context, "block target Gamma long enough content here"),
            });

        Assert.Throws<PatchApplicationException>(() => PatchApplier.ApplyHunkSemantic(source, hunk));
    }

    [Fact]
    public void Semantic_HunkTooShortToFormAStrongAnchor_ThrowsEvenThoughContentExistsInSource()
    {
        // "x = 1;" is present verbatim in the source, just like the moved
        // blocks above — but at 6 characters it is below AnchorDetector's
        // strong-anchor length floor, so BlockBuilder never pairs it up and
        // block-identity search has nothing to go on. This mirrors
        // BlockBuilder's own "too short to be a strong anchor" behavior:
        // semantic mode inherits that limitation as-is, with no hunk-sized
        // special casing required.
        var source = new[]
        {
            "some very long filler line to pad the file aaaa",
            "x = 1;",
            "another very long filler line to pad the file bb",
        };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 1, 1, 1),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Removed, "x = 1;"),
                new(UnifiedDiffLineKind.Added, "x = 2;"),
            });

        Assert.Throws<PatchApplicationException>(() => PatchApplier.ApplyHunkSemantic(source, hunk));
    }

    [Fact]
    public void FindSemanticCandidates_BlockDuplicatedAtTwoLocations_ReturnsEmptyNotTwoCandidates()
    {
        // Naively expected this to surface 2 ambiguous candidates — it does
        // not. AnchorDetector marks a line Weak (not Strong) the moment it
        // appears more than once anywhere in the source (its own FR-016
        // false-positive control), and BlockBuilder only ever pairs Strong
        // anchors. A verbatim-duplicated block therefore has zero lines
        // eligible to become an anchor at either occurrence, so BlockBuilder
        // yields no candidates at all for it — not two. Verified empirically
        // (this test failed with "Expected 2, Actual 0" before being
        // corrected to match reality): under the current architecture, a
        // fully-duplicated block is reported as "no match" by semantic mode,
        // never as "ambiguous, pick one of two". Closing that gap (e.g. by
        // relaxing the Strong-anchor requirement specifically for candidate
        // *discovery*, while keeping it for scoring) is a separate, larger
        // design question — not part of this slice.
        var source = new[]
        {
            "filler line number one long enough content",
            "filler line number two long enough content",
            "block target Alpha long enough content here",
            "block target Beta long enough content here",
            "block target Gamma long enough content here",
            "filler line number five long enough content",
            "filler line number six long enough content",
            "block target Alpha long enough content here",
            "block target Beta long enough content here",
            "block target Gamma long enough content here",
            "filler line number ten long enough content",
        };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "block target Alpha long enough content here"),
                new(UnifiedDiffLineKind.Removed, "block target Beta long enough content here"),
                new(UnifiedDiffLineKind.Added, "block target BETA long enough content here"),
                new(UnifiedDiffLineKind.Context, "block target Gamma long enough content here"),
            });

        var candidates = PatchApplier.FindSemanticCandidates(source, hunk);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindSemanticCandidates_UnambiguousMove_ReturnsSingleCandidateMatchingApplyHunkSemantic()
    {
        var source = new[]
        {
            "filler line number one long enough content",
            "filler line number two long enough content",
            "filler line number three long enough content",
            "filler line number four long enough content",
            "filler line number five long enough content",
            "block target Alpha long enough content here",
            "block target Beta long enough content here",
            "block target Gamma long enough content here",
            "filler line number eight long enough content",
        };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "block target Alpha long enough content here"),
                new(UnifiedDiffLineKind.Removed, "block target Beta long enough content here"),
                new(UnifiedDiffLineKind.Added, "block target BETA long enough content here"),
                new(UnifiedDiffLineKind.Context, "block target Gamma long enough content here"),
            });

        var candidates = PatchApplier.FindSemanticCandidates(source, hunk);

        Assert.Single(candidates);
        Assert.Equal(6, candidates[0].LineNumber);
    }

    [Fact]
    public void FindSemanticCandidates_BlockContentAbsentEverywhere_ReturnsEmpty()
    {
        var source = new[]
        {
            "totally unrelated line content number one",
            "totally unrelated line content number two",
            "totally unrelated line content number three",
        };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 3, 1, 3),
            new UnifiedDiffLine[]
            {
                new(UnifiedDiffLineKind.Context, "block target Alpha long enough content here"),
                new(UnifiedDiffLineKind.Removed, "block target Beta long enough content here"),
                new(UnifiedDiffLineKind.Added, "block target BETA long enough content here"),
                new(UnifiedDiffLineKind.Context, "block target Gamma long enough content here"),
            });

        var candidates = PatchApplier.FindSemanticCandidates(source, hunk);

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindSemanticCandidates_PureInsertionHunk_ReturnsEmpty()
    {
        var source = new[] { "a", "b", "c" };
        var hunk = new UnifiedDiffHunk(
            new UnifiedDiffHunkHeader(1, 0, 1, 1),
            new UnifiedDiffLine[] { new(UnifiedDiffLineKind.Added, "new") });

        var candidates = PatchApplier.FindSemanticCandidates(source, hunk);

        Assert.Empty(candidates);
    }
}
