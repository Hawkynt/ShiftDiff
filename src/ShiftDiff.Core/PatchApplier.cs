namespace ShiftDiff.Core;

public sealed class PatchApplicationException : Exception {
  public PatchApplicationException(string message) : base(message) {
  }
}

public enum PatchApplicationConfidence {
  Exact,
  High,
  Moved
}

public sealed record PatchApplicationResult(IReadOnlyList<string> Lines, PatchApplicationConfidence Confidence);

public sealed record PatchApplicationCandidate(int LineNumber, double Score, Confidence Confidence);

public sealed record PatchFuzzyCandidate(int LineNumber, PatchApplicationConfidence Confidence);

public static class PatchApplier {
  public static IReadOnlyList<string> ApplyHunkExact(IReadOnlyList<string> sourceLines, UnifiedDiffHunk hunk) {
    var oldStartIndex = hunk.Header.OldStart - 1;
    var oldLines = hunk.Lines
        .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Removed)
        .ToList();

    for (var i = 0; i < oldLines.Count; i++) {
      var sourceIndex = oldStartIndex + i;
      if (sourceIndex >= sourceLines.Count || sourceLines[sourceIndex] != oldLines[i].Content) {
        throw new PatchApplicationException(
            $"Hunk context/removed content does not match source at line {sourceIndex + 1}.");
      }
    }

    var newLines = hunk.Lines
        .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Added)
        .Select(line => line.Content);

    var result = new List<string>(sourceLines.Count - oldLines.Count + hunk.Header.NewCount);
    result.AddRange(sourceLines.Take(oldStartIndex));
    result.AddRange(newLines);
    result.AddRange(sourceLines.Skip(oldStartIndex + hunk.Header.OldCount));
    return result;
  }

  public static IReadOnlyList<string> ApplyFileExact(IReadOnlyList<string> sourceLines, UnifiedDiffFile file) {
    var result = sourceLines;
    foreach (var hunk in file.Hunks.Reverse()) {
      result = ApplyHunkExact(result, hunk);
    }

    return result;
  }

  public static IReadOnlyDictionary<string, IReadOnlyList<string>> ApplyPatchExact(
      UnifiedDiffPatch patch, IReadOnlyDictionary<string, IReadOnlyList<string>> sourcesBySourcePath) {
    var result = new Dictionary<string, IReadOnlyList<string>>();
    foreach (var file in patch.Files) {
      var sourcePath = file.Header.SourcePath;
      if (!sourcesBySourcePath.TryGetValue(sourcePath, out var sourceLines)) {
        throw new PatchApplicationException($"No source provided for path '{sourcePath}'.");
      }

      if (!result.TryAdd(file.Header.TargetPath, ApplyFileExact(sourceLines, file))) {
        throw new PatchApplicationException(
            $"Multiple patch files map to the same target path '{file.Header.TargetPath}'.");
      }
    }

    return result;
  }

  public static PatchApplicationResult ApplyHunkFuzzy(IReadOnlyList<string> sourceLines, UnifiedDiffHunk hunk) {
    var oldLines = hunk.Lines
        .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Removed)
        .ToList();
    var newLines = hunk.Lines
        .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Added)
        .Select(line => line.Content)
        .ToList();
    var recordedStartIndex = hunk.Header.OldStart - 1;

    if (oldLines.Count == 0) {
      var insertion = new List<string>(sourceLines.Count + newLines.Count);
      insertion.AddRange(sourceLines.Take(recordedStartIndex));
      insertion.AddRange(newLines);
      insertion.AddRange(sourceLines.Skip(recordedStartIndex));
      return new PatchApplicationResult(insertion, PatchApplicationConfidence.Exact);
    }

    var match = FindClosestMatch(sourceLines, oldLines, recordedStartIndex);
    if (match is null) {
      throw new PatchApplicationException(
          "Hunk context/removed content was not found anywhere in the source.");
    }

    var matchStartIndex = match.Value.Index;
    var confidence = match.Value.Kind == MatchKind.Exact && matchStartIndex == recordedStartIndex
        ? PatchApplicationConfidence.Exact
        : PatchApplicationConfidence.High;

    // A leading/trailing Context line matched only via drift tolerance was
    // never actually verified against the source at this position — the
    // hunk's recorded content for that line is spliced through unchanged
    // elsewhere, so here we must keep the source's real (unverified) line
    // instead of overwriting it with the hunk's recorded Context content.
    var linesToInsert = newLines;
    if (match.Value.Kind == MatchKind.LeadingDrift) {
      linesToInsert = new List<string>(newLines) { [0] = sourceLines[matchStartIndex] };
    } else if (match.Value.Kind == MatchKind.TrailingDrift) {
      linesToInsert = new List<string>(newLines);
      linesToInsert[^1] = sourceLines[matchStartIndex + oldLines.Count - 1];
    }

    var result = new List<string>(sourceLines.Count - oldLines.Count + linesToInsert.Count);
    result.AddRange(sourceLines.Take(matchStartIndex));
    result.AddRange(linesToInsert);
    result.AddRange(sourceLines.Skip(matchStartIndex + oldLines.Count));
    return new PatchApplicationResult(result, confidence);
  }

  public static PatchApplicationResult ApplyFileFuzzy(IReadOnlyList<string> sourceLines, UnifiedDiffFile file) {
    IReadOnlyList<string> lines = sourceLines;
    var confidence = PatchApplicationConfidence.Exact;
    foreach (var hunk in file.Hunks.Reverse()) {
      var hunkResult = ApplyHunkFuzzy(lines, hunk);
      lines = hunkResult.Lines;
      if (hunkResult.Confidence == PatchApplicationConfidence.High) {
        confidence = PatchApplicationConfidence.High;
      }
    }

    return new PatchApplicationResult(lines, confidence);
  }

  // FR-023 second slice: fuzzy mode's counterpart to FindSemanticCandidates.
  // Unlike block-identity matching, MatchAt has no anchor-uniqueness gate at
  // all, so a verbatim-duplicated block genuinely surfaces as 2+ candidates
  // here — the ambiguous-duplicate scenario FR-023 describes, which semantic
  // mode structurally cannot detect. Deliberately additive: ApplyHunkFuzzy/
  // ApplyFileFuzzy keep using FindClosestMatch and are untouched.
  public static IReadOnlyList<PatchFuzzyCandidate> FindFuzzyCandidates(
      IReadOnlyList<string> sourceLines, UnifiedDiffHunk hunk) {
    var oldLines = hunk.Lines
        .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Removed)
        .ToList();
    if (oldLines.Count == 0) {
      return Array.Empty<PatchFuzzyCandidate>();
    }

    var preferredIndex = hunk.Header.OldStart - 1;
    return FindAllMatches(sourceLines, oldLines)
        .OrderBy(match => match.Kind == MatchKind.Exact ? 0 : 1)
        .ThenBy(match => Math.Abs(match.Index - preferredIndex))
        .Select(match => new PatchFuzzyCandidate(
            match.Index + 1,
            match.Kind == MatchKind.Exact ? PatchApplicationConfidence.Exact : PatchApplicationConfidence.High))
        .ToList();
  }

  public static PatchApplicationResult ApplyHunkSemantic(
      IReadOnlyList<string> sourceLines, UnifiedDiffHunk hunk, DetectionMode mode = DetectionMode.Balanced, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();

    var oldLines = hunk.Lines
        .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Removed)
        .ToList();
    var newLines = hunk.Lines
        .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Added)
        .Select(line => line.Content)
        .ToList();

    if (oldLines.Count == 0) {
      var recordedStartIndex = hunk.Header.OldStart - 1;
      var insertion = new List<string>(sourceLines.Count + newLines.Count);
      insertion.AddRange(sourceLines.Take(recordedStartIndex));
      insertion.AddRange(newLines);
      insertion.AddRange(sourceLines.Skip(recordedStartIndex));
      return new PatchApplicationResult(insertion, PatchApplicationConfidence.Exact);
    }

    var hunkOldContent = oldLines.Select(line => line.Content).ToArray();
    var sourceArray = sourceLines as string[] ?? sourceLines.ToArray();

    var match = FindBestBlockMatch(hunkOldContent, sourceArray, hunk.Header.OldStart - 1, mode, cancellationToken);
    if (match is null) {
      throw new PatchApplicationException(
          "Hunk context/removed content was not found anywhere in the source, including via block-identity matching.");
    }

    var matchStartIndex = match.Value;
    var result = new List<string>(sourceLines.Count - hunkOldContent.Length + newLines.Count);
    result.AddRange(sourceLines.Take(matchStartIndex));
    result.AddRange(newLines);
    result.AddRange(sourceLines.Skip(matchStartIndex + hunkOldContent.Length));
    return new PatchApplicationResult(result, PatchApplicationConfidence.Moved);
  }

  // FR-023 (Patch Conflict Handling), first slice: surfaces every viable
  // block-identity match instead of silently committing to the single best
  // one the way ApplyHunkSemantic does. A caller (CLI/UI, not this library)
  // uses this to detect ambiguity — 2+ candidates — and drive "mark hunk as
  // uncertain, show candidate locations, explain why confidence is low".
  // Deliberately additive: ApplyHunkSemantic/ApplyFileSemantic are untouched,
  // so nothing that already depends on their throw-or-single-pick contract
  // changes. Skip/manual-edit/select-location are UI concerns and stay out
  // of Core's scope. Pure insertions (no Removed/Context lines) have no
  // "location" to be ambiguous about, so they yield no candidates.
  public static IReadOnlyList<PatchApplicationCandidate> FindSemanticCandidates(
      IReadOnlyList<string> sourceLines, UnifiedDiffHunk hunk, DetectionMode mode = DetectionMode.Balanced, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();

    var oldLines = hunk.Lines
        .Where(line => line.Kind is UnifiedDiffLineKind.Context or UnifiedDiffLineKind.Removed)
        .ToList();
    if (oldLines.Count == 0) {
      return Array.Empty<PatchApplicationCandidate>();
    }

    var hunkOldContent = oldLines.Select(line => line.Content).ToArray();
    var sourceArray = sourceLines as string[] ?? sourceLines.ToArray();

    return FindBlockMatchCandidates(hunkOldContent, sourceArray, mode, cancellationToken)
        .OrderByDescending(candidate => candidate.Score)
        .Select(candidate => new PatchApplicationCandidate(
            candidate.StartIndex + 1, candidate.Score, ConfidenceClassifier.Classify(candidate.Score)))
        .ToList();
  }

  public static PatchApplicationResult ApplyFileSemantic(
      IReadOnlyList<string> sourceLines, UnifiedDiffFile file, DetectionMode mode = DetectionMode.Balanced, CancellationToken cancellationToken = default) {
    IReadOnlyList<string> lines = sourceLines;
    var confidence = PatchApplicationConfidence.Exact;
    foreach (var hunk in file.Hunks.Reverse()) {
      cancellationToken.ThrowIfCancellationRequested();
      var hunkResult = ApplyHunkSemantic(lines, hunk, mode, cancellationToken);
      lines = hunkResult.Lines;
      if (hunkResult.Confidence == PatchApplicationConfidence.Moved) {
        confidence = PatchApplicationConfidence.Moved;
      } else if (hunkResult.Confidence == PatchApplicationConfidence.High
                  && confidence == PatchApplicationConfidence.Exact) {
        confidence = PatchApplicationConfidence.High;
      }
    }

    return new PatchApplicationResult(lines, confidence);
  }

  // BlockBuilder only pairs up anchor lines individually — for a hunk-sized
  // fragment some of its own lines may be too short or duplicated to ever
  // qualify as a strong anchor (see AnchorDetector), so a returned
  // BlockCandidate may span only part of the fragment rather than all of
  // it. Each candidate still pins down where the fragment as a whole would
  // begin in the source (NewStart - OldStart); we re-score that whole
  // fixed-length window with BlockSimilarityScorer rather than trusting the
  // candidate's own (possibly partial) span, so partial anchor coverage
  // doesn't understate a genuinely strong content match. If not a single
  // line in the fragment is anchor-worthy, BlockBuilder yields no
  // candidates at all and there is no position to recover — semantic mode
  // has nothing left to try and reports no match, same as fuzzy mode does.
  private static int? FindBestBlockMatch(
      string[] hunkOldContent, string[] sourceArray, int preferredIndex, DetectionMode mode, CancellationToken cancellationToken = default) {
    int? bestStartIndex = null;
    var bestScore = -1.0;
    var bestDistance = int.MaxValue;

    foreach (var candidate in FindBlockMatchCandidates(hunkOldContent, sourceArray, mode, cancellationToken)) {
      var distance = Math.Abs(candidate.StartIndex - preferredIndex);
      if (candidate.Score > bestScore || (candidate.Score == bestScore && distance < bestDistance)) {
        bestScore = candidate.Score;
        bestDistance = distance;
        bestStartIndex = candidate.StartIndex;
      }
    }

    return bestStartIndex;
  }

  private readonly record struct BlockMatchCandidate(int StartIndex, double Score);

  private static List<BlockMatchCandidate> FindBlockMatchCandidates(
      string[] hunkOldContent, string[] sourceArray, DetectionMode mode, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();

    var results = new List<BlockMatchCandidate>();

    var length = hunkOldContent.Length;
    var lastPossibleStart = sourceArray.Length - length;
    if (lastPossibleStart < 0) {
      return results;
    }

    // Fragment-in-file search, not full-document comparison — see
    // BlockBuilder.Build's excludeSamePosition doc comment.
    var candidates = BlockBuilder.Build(hunkOldContent, sourceArray, excludeSamePosition: false, cancellationToken: cancellationToken);
    if (candidates.Length == 0) {
      return results;
    }

    var threshold = DetectionModeThresholds.MovedConfidenceThreshold(mode);
    var consideredStartIndices = new HashSet<int>();
    var oldAnchors = AnchorDetector.Detect(hunkOldContent);
    var newAnchors = AnchorDetector.Detect(sourceArray);
    var oldHashesRaw = hunkOldContent.Select(LineHasher.HashRaw).ToArray();
    var newHashesRaw = sourceArray.Select(LineHasher.HashRaw).ToArray();
    var oldHashesNormalized = hunkOldContent.Select(LineHasher.HashWhitespaceNormalized).ToArray();
    var newHashesNormalized = sourceArray.Select(LineHasher.HashWhitespaceNormalized).ToArray();

    foreach (var candidate in candidates) {
      cancellationToken.ThrowIfCancellationRequested();
      var startIndex = candidate.NewStart - candidate.OldStart;
      if (startIndex < 0 || startIndex > lastPossibleStart || !consideredStartIndices.Add(startIndex)) {
        continue;
      }

      var fullSpanCandidate = new BlockCandidate(0, length - 1, startIndex, startIndex + length - 1);
      var score = BlockSimilarityScorer.CombinedScore(
          fullSpanCandidate,
          hunkOldContent,
          sourceArray,
          oldHashesRaw,
          newHashesRaw,
          oldHashesNormalized,
          newHashesNormalized,
          oldAnchors,
          newAnchors);
      if (score < threshold || ConfidenceClassifier.Classify(score) == Confidence.Rejected) {
        continue;
      }

      results.Add(new BlockMatchCandidate(startIndex, score));
    }

    return results;
  }

  private enum MatchKind {
    Exact,
    LeadingDrift,
    TrailingDrift,
  }

  private readonly record struct FuzzyMatch(int Index, MatchKind Kind);

  private static FuzzyMatch? FindClosestMatch(
      IReadOnlyList<string> sourceLines, IReadOnlyList<UnifiedDiffLine> oldLines, int preferredIndex) {
    FuzzyMatch? best = null;
    var bestDistance = int.MaxValue;
    foreach (var match in FindAllMatches(sourceLines, oldLines)) {
      var distance = Math.Abs(match.Index - preferredIndex);
      if (distance < bestDistance) {
        bestDistance = distance;
        best = match;
      }
    }

    return best;
  }

  private static List<FuzzyMatch> FindAllMatches(
      IReadOnlyList<string> sourceLines, IReadOnlyList<UnifiedDiffLine> oldLines) {
    var results = new List<FuzzyMatch>();
    var lastPossibleStart = sourceLines.Count - oldLines.Count;
    if (lastPossibleStart < 0) {
      return results;
    }

    var leadingContextRun = CountLeadingContextRun(oldLines);
    var trailingContextRun = CountTrailingContextRun(oldLines);

    for (var candidate = 0; candidate <= lastPossibleStart; candidate++) {
      var kind = MatchAt(sourceLines, oldLines, candidate, leadingContextRun, trailingContextRun);
      if (kind is not null) {
        results.Add(new FuzzyMatch(candidate, kind.Value));
      }
    }

    return results;
  }

  // Counts Context-kind lines before the first Removed-kind line — the
  // leading edge fuzz tolerance may relax at most this many lines (fuzz
  // level 1 here means at most the single outermost one).
  private static int CountLeadingContextRun(IReadOnlyList<UnifiedDiffLine> oldLines) {
    var count = 0;
    while (count < oldLines.Count && oldLines[count].Kind == UnifiedDiffLineKind.Context) {
      count++;
    }

    return count;
  }

  // Counts Context-kind lines after the last Removed-kind line — the
  // trailing counterpart of CountLeadingContextRun.
  private static int CountTrailingContextRun(IReadOnlyList<UnifiedDiffLine> oldLines) {
    var count = 0;
    while (count < oldLines.Count && oldLines[oldLines.Count - 1 - count].Kind == UnifiedDiffLineKind.Context) {
      count++;
    }

    return count;
  }

  private static MatchKind? MatchAt(
      IReadOnlyList<string> sourceLines, IReadOnlyList<UnifiedDiffLine> oldLines, int start,
      int leadingContextRun, int trailingContextRun) {
    if (MatchesAtRange(sourceLines, oldLines, start, 0, oldLines.Count)) {
      return MatchKind.Exact;
    }

    // Fuzz level 1: relax at most the single outermost Context line on
    // one edge at a time (never both edges together, never a Removed
    // line — Removed lines are the actual change and must always match).
    if (leadingContextRun > 0 && MatchesAtRange(sourceLines, oldLines, start, 1, oldLines.Count)) {
      return MatchKind.LeadingDrift;
    }

    if (trailingContextRun > 0 && MatchesAtRange(sourceLines, oldLines, start, 0, oldLines.Count - 1)) {
      return MatchKind.TrailingDrift;
    }

    return null;
  }

  private static bool MatchesAtRange(
      IReadOnlyList<string> sourceLines, IReadOnlyList<UnifiedDiffLine> oldLines, int start, int fromInclusive, int toExclusive) {
    for (var i = fromInclusive; i < toExclusive; i++) {
      if (sourceLines[start + i] != oldLines[i].Content) {
        return false;
      }
    }

    return true;
  }
}
