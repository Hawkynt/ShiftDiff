namespace ShiftDiff.Core;

public static class BlockBuilder {
  // excludeSamePosition assumes oldLines/newLines share one coordinate
  // space (two versions of the same document) — an anchor at the same
  // index in both really did stay put, so FileComparer's real move
  // detection correctly skips it. PatchApplier's fragment-search reuse
  // (searching a small hunk-local array within a whole source file) has no
  // such shared coordinate space: the fragment's own indices always start
  // at 0, so a match landing at that same raw source index is a
  // coincidence, not evidence the content "didn't move" — excluding it
  // there silently dropped the only valid candidate for any single-line
  // hunk whose target happened to sit at that source index.
  public static BlockCandidate[] Build(string[] oldLines, string[] newLines, bool excludeSamePosition = true, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();

    var oldAnchors = AnchorDetector.Detect(oldLines, cancellationToken);
    var newAnchors = AnchorDetector.Detect(newLines, cancellationToken);

    // Hash each line once here rather than recalling LineHasher.Hash per
    // anchor (Detect already hashed every line internally, but doesn't
    // expose the result) — avoids a second full-file SHA-256 pass per side.
    var oldHashes = oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();
    var newHashes = newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();

    var newStrongAnchorIndicesByHash = newAnchors
        .Where(anchor => anchor.Quality == AnchorQuality.Strong)
        .ToDictionary(
            anchor => newHashes[anchor.Index],
            anchor => anchor.Index);

    var rawMatchPairs = new List<(int OldIndex, int NewIndex)>();

    foreach (var oldAnchor in oldAnchors.Where(anchor => anchor.Quality == AnchorQuality.Strong)) {
      var oldHash = oldHashes[oldAnchor.Index];

      if (!newStrongAnchorIndicesByHash.TryGetValue(oldHash, out var newIndex)
          || (excludeSamePosition && newIndex == oldAnchor.Index)) {
        continue;
      }

      rawMatchPairs.Add((oldAnchor.Index, newIndex));
    }

    rawMatchPairs.Sort((left, right) => left.OldIndex.CompareTo(right.OldIndex));

    var candidates = new List<BlockCandidate>();

    for (var index = 0; index < rawMatchPairs.Count; index++) {
      var firstPair = rawMatchPairs[index];
      var lastPair = firstPair;

      while (index + 1 < rawMatchPairs.Count) {
        var nextPair = rawMatchPairs[index + 1];

        if (nextPair.OldIndex != lastPair.OldIndex + 1 || nextPair.NewIndex != lastPair.NewIndex + 1) {
          break;
        }

        lastPair = nextPair;
        index++;
      }

      candidates.Add(new BlockCandidate(
          firstPair.OldIndex,
          lastPair.OldIndex,
          firstPair.NewIndex,
          lastPair.NewIndex));
    }

    return candidates.ToArray();
  }
}
