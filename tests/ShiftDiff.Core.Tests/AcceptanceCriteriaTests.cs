using System.Text;
using ShiftDiff.Core;

namespace ShiftDiff.Core.Tests;

// The acceptance criteria from SPEC section 19, exercised end to end against the
// engine rather than against a single stage of it.
public class AcceptanceCriteriaTests
{
    // AC-001: a block of at least 10 meaningful lines moved, with 20% of its
    // lines edited, is classified as moved (or moved+edited) with at least
    // likely confidence in balanced mode.
    [Fact]
    public void AC001_MovedAndEditedBlock_IsReportedWithAtLeastLikelyConfidence()
    {
        var block = new[]
        {
            "    public decimal CalculateTotal(Order order)",
            "    {",
            "        var subtotal = 0m;",
            "        foreach (var line in order.Lines)",
            "        {",
            "            subtotal += line.UnitPrice * line.Quantity;",
            "        }",
            "        var discount = subtotal * order.DiscountRate;",
            "        var shipping = order.IsExpress ? 12.50m : 4.90m;",
            "        var tax = (subtotal - discount) * TaxRate;",
            "        return subtotal - discount + shipping + tax;",
            "    }",
        };

        var prologue = new[] { "using System;", "", "public sealed class Billing", "{", "    private const decimal TaxRate = 0.19m;", "" };
        var otherMethod = new[]
        {
            "",
            "    public string Describe(Order order)",
            "    {",
            "        return $\"order {order.Id} with {order.Lines.Count} lines\";",
            "    }",
            "}",
        };

        // The block moves after the other method, and two of its twelve lines change.
        var editedBlock = block.ToArray();
        editedBlock[7] = "        var discount = subtotal * order.DiscountRate * LoyaltyFactor;";
        editedBlock[8] = "        var shipping = order.IsExpress ? 14.00m : 5.90m;";

        var oldText = string.Join('\n', prologue.Concat(block).Concat(otherMethod));
        var newText = string.Join('\n', prologue.Concat(otherMethod.SkipLast(1)).Concat(editedBlock).Append("}"));

        var result = FileComparer.Compare(
            Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText), mode: DetectionMode.Balanced);
        var moved = MoveRefiner.Refine(result.MovedBlocks);

        Assert.NotEmpty(moved);
        var relocation = moved.MaxBy(candidate => candidate.OldEnd - candidate.OldStart)!;
        Assert.True(
            relocation.MatchType is ChangeType.Moved or ChangeType.MovedEdited,
            $"expected a relocation, got {relocation.MatchType}");
        Assert.True(
            relocation.Confidence is Confidence.Certain or Confidence.Likely,
            $"expected at least likely confidence, got {relocation.Confidence}");
    }

    // AC-002: braces, blank lines and imports must not be enough on their own to
    // invent a moved block.
    [Fact]
    public void AC002_FilesFullOfBoilerplate_ProduceNoMovedBlocks()
    {
        var oldText = string.Join('\n', Enumerable.Range(0, 40).SelectMany(i => new[] { "{", "", "}", "using System;" }));
        var newText = string.Join('\n', Enumerable.Range(0, 40).SelectMany(i => new[] { "{", "", "}", "using System;" })
            .Append("using System.Text;"));

        var result = FileComparer.Compare(Encoding.UTF8.GetBytes(oldText), Encoding.UTF8.GetBytes(newText));

        Assert.Empty(MoveRefiner.Refine(result.MovedBlocks));
    }

    // AC-004: source plus a compatible unified diff reconstructs the target exactly.
    [Fact]
    public void AC004_PatchReconstruction_ReproducesTheTargetExactly()
    {
        var oldLines = new[] { "alpha", "beta", "gamma", "delta", "epsilon" };
        var newLines = new[] { "alpha", "BETA", "gamma", "delta", "epsilon", "zeta" };

        var result = FileComparer.Compare(
            Encoding.UTF8.GetBytes(string.Join('\n', oldLines)),
            Encoding.UTF8.GetBytes(string.Join('\n', newLines)));
        var patch = UnifiedDiffBuilder.Build(result.Changes, "old.txt", "new.txt");
        var reparsed = UnifiedDiffParser.ParseFile(UnifiedDiffFormatter.Format(patch));

        Assert.Equal(newLines, PatchApplier.ApplyFileExact(oldLines, reparsed));
    }

    // AC-005: a hunk whose recorded position is stale still applies in fuzzy mode.
    [Fact]
    public void AC005_FuzzyReconstruction_AppliesAShiftedHunkWithHighConfidence()
    {
        var oldLines = new[] { "alpha", "beta", "gamma" };
        var newLines = new[] { "alpha", "BETA", "gamma" };
        var result = FileComparer.Compare(
            Encoding.UTF8.GetBytes(string.Join('\n', oldLines)),
            Encoding.UTF8.GetBytes(string.Join('\n', newLines)));
        var patch = UnifiedDiffBuilder.Build(result.Changes, "old.txt", "new.txt");

        var shiftedSource = new[] { "header", "header", "alpha", "beta", "gamma" };
        var applied = PatchApplier.ApplyFileFuzzy(shiftedSource, patch);

        Assert.Contains("BETA", applied.Lines);
        Assert.NotEqual(PatchApplicationConfidence.Exact, applied.Confidence);
    }
}
