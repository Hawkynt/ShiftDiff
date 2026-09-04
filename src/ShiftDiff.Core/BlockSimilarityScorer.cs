using System.Numerics;
using System.Text;

namespace ShiftDiff.Core;

public static class BlockSimilarityScorer {
  private const int FingerprintBits = 64;
  private const int ShingleSize = 3;

  public static int TokenCount(BlockCandidate candidate, string[] oldLines, string[] newLines) =>
      TokenizeRange(oldLines, candidate.OldStart, candidate.OldEnd).Count;

  public static double ExactHashOverlap(BlockCandidate candidate, string[] oldLines, string[] newLines) =>
      ExactHashOverlapFromHashes(
          candidate,
          oldLines.Select(LineHasher.HashRaw).ToArray(),
          newLines.Select(LineHasher.HashRaw).ToArray());

  /// <summary>
  /// Variant of <see cref="ExactHashOverlap(BlockCandidate, string[], string[])"/> taking already-computed
  /// raw-line hashes for the whole file — avoids re-hashing every line once per candidate when a caller
  /// (e.g. <see cref="BlockClassifier.Classify"/>) scores many candidates against the same fixed
  /// oldLines/newLines. Named rather than overloaded because a same-signature overload distinguished only
  /// by parameter name (string[] hashes vs. string[] lines) is not valid C#.
  /// </summary>
  public static double ExactHashOverlapFromHashes(BlockCandidate candidate, string[] oldHashesRaw, string[] newHashesRaw) {
    var lineCount = candidate.OldEnd - candidate.OldStart + 1;
    var matchingLines = 0;

    for (var offset = 0; offset < lineCount; offset++) {
      if (oldHashesRaw[candidate.OldStart + offset] == newHashesRaw[candidate.NewStart + offset]) {
        matchingLines++;
      }
    }

    return matchingLines / (double)lineCount;
  }

  public static double NormalizedHashOverlap(BlockCandidate candidate, string[] oldLines, string[] newLines) =>
      NormalizedHashOverlapFromHashes(
          candidate,
          oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray(),
          newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray());

  /// <summary>See <see cref="ExactHashOverlapFromHashes"/> — whitespace-normalized tier only.</summary>
  public static double NormalizedHashOverlapFromHashes(BlockCandidate candidate, string[] oldHashesNormalized, string[] newHashesNormalized) {
    var lineCount = candidate.OldEnd - candidate.OldStart + 1;
    var matchingLines = 0;

    for (var offset = 0; offset < lineCount; offset++) {
      if (oldHashesNormalized[candidate.OldStart + offset] == newHashesNormalized[candidate.NewStart + offset]) {
        matchingLines++;
      }
    }

    return matchingLines / (double)lineCount;
  }

  public static double TokenShingleSimilarity(BlockCandidate candidate, string[] oldLines, string[] newLines) {
    var oldShingles = BuildShingles(TokenizeRange(oldLines, candidate.OldStart, candidate.OldEnd));
    var newShingles = BuildShingles(TokenizeRange(newLines, candidate.NewStart, candidate.NewEnd));

    if (oldShingles.Count == 0 && newShingles.Count == 0) {
      return 1.0;
    }

    var intersectionCount = oldShingles.Intersect(newShingles).Count();
    var unionCount = oldShingles.Union(newShingles).Count();

    return intersectionCount / (double)unionCount;
  }

  public static double SimHashSimilarity(BlockCandidate candidate, string[] oldLines, string[] newLines) {
    var oldTokens = TokenizeRange(oldLines, candidate.OldStart, candidate.OldEnd);
    var newTokens = TokenizeRange(newLines, candidate.NewStart, candidate.NewEnd);

    if (oldTokens.Count == 0 && newTokens.Count == 0) {
      return 1.0;
    }

    if (oldTokens.Count == 0 || newTokens.Count == 0) {
      return 0.0;
    }

    var oldFingerprint = BuildFingerprint(oldTokens);
    var newFingerprint = BuildFingerprint(newTokens);
    var distance = HammingDistance(oldFingerprint, newFingerprint);

    return 1.0 - (distance / (double)FingerprintBits);
  }

  public readonly record struct FileFingerprint(HashSet<string> Shingles, ulong SimHash, int TokenCount);

  /// <summary>
  /// Precomputes the token shingle set and SimHash fingerprint for a whole file so a caller comparing
  /// one file against many candidates (e.g. <see cref="FolderRenameDetector"/>) doesn't re-tokenize and
  /// re-hash the same file once per candidate pair. Same precomputed rationale as
  /// <see cref="ExactHashOverlapFromHashes"/>.
  /// </summary>
  public static FileFingerprint ComputeFileFingerprint(string[] lines) {
    var tokens = TokenizeRange(lines, 0, lines.Length - 1);
    return new FileFingerprint(BuildShingles(tokens), BuildFingerprint(tokens), tokens.Count);
  }

  /// <summary>See <see cref="TokenShingleSimilarity"/> — precomputed-fingerprint overload.</summary>
  public static double TokenShingleSimilarityFromFingerprint(FileFingerprint oldFingerprint, FileFingerprint newFingerprint) {
    if (oldFingerprint.Shingles.Count == 0 && newFingerprint.Shingles.Count == 0) {
      return 1.0;
    }

    var intersectionCount = oldFingerprint.Shingles.Intersect(newFingerprint.Shingles).Count();
    var unionCount = oldFingerprint.Shingles.Union(newFingerprint.Shingles).Count();
    return intersectionCount / (double)unionCount;
  }

  /// <summary>See <see cref="SimHashSimilarity"/> — precomputed-fingerprint overload.</summary>
  public static double SimHashSimilarityFromFingerprint(FileFingerprint oldFingerprint, FileFingerprint newFingerprint) {
    if (oldFingerprint.TokenCount == 0 && newFingerprint.TokenCount == 0) {
      return 1.0;
    }

    if (oldFingerprint.TokenCount == 0 || newFingerprint.TokenCount == 0) {
      return 0.0;
    }

    var distance = HammingDistance(oldFingerprint.SimHash, newFingerprint.SimHash);
    return 1.0 - (distance / (double)FingerprintBits);
  }

  public static double BlockSizeRatio(BlockCandidate candidate, string[] oldLines, string[] newLines) {
    var oldLineCount = candidate.OldEnd - candidate.OldStart + 1;
    var newLineCount = candidate.NewEnd - candidate.NewStart + 1;

    var minLineCount = Math.Min(oldLineCount, newLineCount);
    var maxLineCount = Math.Max(oldLineCount, newLineCount);

    return minLineCount / (double)maxLineCount;
  }

  public static double OrderingConsistency(BlockCandidate candidate, string[] oldLines, string[] newLines) =>
      OrderingConsistencyFromHashes(
          candidate,
          oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray(),
          newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray());

  /// <summary>See <see cref="ExactHashOverlapFromHashes"/> — same precomputed-array rationale.</summary>
  public static double OrderingConsistencyFromHashes(BlockCandidate candidate, string[] oldHashesNormalized, string[] newHashesNormalized) {
    var oldHashes = oldHashesNormalized[candidate.OldStart..(candidate.OldEnd + 1)];
    var newHashes = newHashesNormalized[candidate.NewStart..(candidate.NewEnd + 1)];

    var oldHashCounts = oldHashes.GroupBy(hash => hash).ToDictionary(group => group.Key, group => group.Count());
    var newHashCounts = newHashes.GroupBy(hash => hash).ToDictionary(group => group.Key, group => group.Count());
    var newOffsetByHash = newHashes
        .Select((hash, offset) => (hash, offset))
        .GroupBy(pair => pair.hash)
        .ToDictionary(group => group.Key, group => group.First().offset);

    var matchedNewOffsetsInOldOrder = new List<int>();

    foreach (var oldHash in oldHashes) {
      if (oldHashCounts[oldHash] == 1 && newHashCounts.TryGetValue(oldHash, out var newCount) && newCount == 1) {
        matchedNewOffsetsInOldOrder.Add(newOffsetByHash[oldHash]);
      }
    }

    if (matchedNewOffsetsInOldOrder.Count < 2) {
      return 1.0;
    }

    var concordantPairs = 0;
    var totalPairs = 0;

    for (var i = 0; i < matchedNewOffsetsInOldOrder.Count; i++) {
      for (var j = i + 1; j < matchedNewOffsetsInOldOrder.Count; j++) {
        totalPairs++;
        if (matchedNewOffsetsInOldOrder[j] > matchedNewOffsetsInOldOrder[i]) {
          concordantPairs++;
        }
      }
    }

    return concordantPairs / (double)totalPairs;
  }

  public static double RarityWeightedAnchorScore(BlockCandidate candidate, string[] oldLines, string[] newLines) =>
      RarityWeightedAnchorScore(candidate, AnchorDetector.Detect(oldLines), AnchorDetector.Detect(newLines));

  /// <summary>
  /// Overload taking already-computed anchors — avoids re-running <see cref="AnchorDetector.Detect"/>
  /// (an O(n) pass) once per candidate when a caller (e.g. <see cref="BlockClassifier.Classify"/>)
  /// scores many candidates against the same fixed oldLines/newLines.
  /// </summary>
  public static double RarityWeightedAnchorScore(BlockCandidate candidate, LineAnchor[] oldAnchors, LineAnchor[] newAnchors) {
    var oldStrongFraction = StrongFraction(oldAnchors, candidate.OldStart, candidate.OldEnd);
    var newStrongFraction = StrongFraction(newAnchors, candidate.NewStart, candidate.NewEnd);

    return (oldStrongFraction + newStrongFraction) / 2.0;
  }

  private static double StrongFraction(LineAnchor[] anchors, int start, int end) {
    var count = end - start + 1;
    var strongCount = 0;

    for (var offset = 0; offset < count; offset++) {
      if (anchors[start + offset].Quality == AnchorQuality.Strong) {
        strongCount++;
      }
    }

    return strongCount / (double)count;
  }

  public static double NeighboringBlockConsistency(BlockCandidate candidate, string[] oldLines, string[] newLines) =>
      NeighboringBlockConsistencyFromHashes(
          candidate,
          oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray(),
          newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray());

  /// <summary>See <see cref="ExactHashOverlapFromHashes"/> — same precomputed-array rationale.</summary>
  public static double NeighboringBlockConsistencyFromHashes(BlockCandidate candidate, string[] oldHashesNormalized, string[] newHashesNormalized) {
    var comparableCount = 0;
    var matchingCount = 0;

    if (candidate.OldStart > 0 && candidate.NewStart > 0) {
      comparableCount++;

      if (oldHashesNormalized[candidate.OldStart - 1] == newHashesNormalized[candidate.NewStart - 1]) {
        matchingCount++;
      }
    }

    if (candidate.OldEnd < oldHashesNormalized.Length - 1 && candidate.NewEnd < newHashesNormalized.Length - 1) {
      comparableCount++;

      if (oldHashesNormalized[candidate.OldEnd + 1] == newHashesNormalized[candidate.NewEnd + 1]) {
        matchingCount++;
      }
    }

    if (comparableCount == 0) {
      return 1.0;
    }

    return matchingCount / (double)comparableCount;
  }

  public static double CombinedScore(BlockCandidate candidate, string[] oldLines, string[] newLines) =>
      CombinedScore(candidate, oldLines, newLines, AnchorDetector.Detect(oldLines), AnchorDetector.Detect(newLines));

  /// <summary>
  /// Overload taking already-computed anchors — see <see cref="RarityWeightedAnchorScore(BlockCandidate, LineAnchor[], LineAnchor[])"/>.
  /// </summary>
  public static double CombinedScore(BlockCandidate candidate, string[] oldLines, string[] newLines, LineAnchor[] oldAnchors, LineAnchor[] newAnchors) {
    var oldHashesRaw = oldLines.Select(LineHasher.HashRaw).ToArray();
    var newHashesRaw = newLines.Select(LineHasher.HashRaw).ToArray();
    var oldHashesNormalized = oldLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();
    var newHashesNormalized = newLines.Select(LineHasher.HashWhitespaceNormalized).ToArray();

    return CombinedScore(
        candidate,
        oldLines,
        newLines,
        oldHashesRaw,
        newHashesRaw,
        oldHashesNormalized,
        newHashesNormalized,
        oldAnchors,
        newAnchors);
  }

  /// <summary>
  /// Fully precomputed overload — combines the anchor precompute above with precomputed raw and
  /// whitespace-normalized hash arrays for the whole file, so a caller scoring many candidates against
  /// the same fixed oldLines/newLines (e.g. <see cref="BlockClassifier.Classify"/>) hashes each line once
  /// instead of once per candidate. TokenShingleSimilarity/SimHashSimilarity/BlockSizeRatio are
  /// tokenize-based rather than hash-based and still take oldLines/newLines directly.
  /// </summary>
  public static double CombinedScore(
      BlockCandidate candidate,
      string[] oldLines,
      string[] newLines,
      string[] oldHashesRaw,
      string[] newHashesRaw,
      string[] oldHashesNormalized,
      string[] newHashesNormalized,
      LineAnchor[] oldAnchors,
      LineAnchor[] newAnchors) =>
      (ExactHashOverlapFromHashes(candidate, oldHashesRaw, newHashesRaw)
       + NormalizedHashOverlapFromHashes(candidate, oldHashesNormalized, newHashesNormalized)
       + TokenShingleSimilarity(candidate, oldLines, newLines)
       + SimHashSimilarity(candidate, oldLines, newLines)
       + BlockSizeRatio(candidate, oldLines, newLines)
       + OrderingConsistencyFromHashes(candidate, oldHashesNormalized, newHashesNormalized)
       + RarityWeightedAnchorScore(candidate, oldAnchors, newAnchors)
       + NeighboringBlockConsistencyFromHashes(candidate, oldHashesNormalized, newHashesNormalized)) / 8.0;

  private static List<string> Tokenize(string line) {
    var tokens = new List<string>();
    var builder = new StringBuilder(line.Length);

    foreach (var character in line) {
      if (char.IsLetterOrDigit(character) || character == '_') {
        builder.Append(character);
        continue;
      }

      if (builder.Length > 0) {
        tokens.Add(builder.ToString());
        builder.Clear();
      }
    }

    if (builder.Length > 0) {
      tokens.Add(builder.ToString());
    }

    return tokens;
  }

  private static List<string> TokenizeRange(string[] lines, int start, int end) {
    var tokens = new List<string>();

    for (var index = start; index <= end; index++) {
      tokens.AddRange(Tokenize(lines[index]));
    }

    return tokens;
  }

  private static HashSet<string> BuildShingles(List<string> tokens) {
    var shingles = new HashSet<string>();

    if (tokens.Count == 0) {
      return shingles;
    }

    if (tokens.Count < ShingleSize) {
      shingles.Add(string.Join(' ', tokens));
      return shingles;
    }

    for (var index = 0; index <= tokens.Count - ShingleSize; index++) {
      shingles.Add(string.Join(' ', tokens.Skip(index).Take(ShingleSize)));
    }

    return shingles;
  }

  private static ulong HashToken(string token) {
    var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token));

    return BitConverter.ToUInt64(hash, 0);
  }

  private static ulong BuildFingerprint(List<string> tokens) {
    var frequencies = tokens
        .GroupBy(token => token)
        .Select(group => new {
          Token = group.Key,
          Weight = group.Count(),
        });

    ulong fingerprint = 0;

    for (var bit = 0; bit < FingerprintBits; bit++) {
      var sum = 0;

      foreach (var frequency in frequencies) {
        var tokenHash = HashToken(frequency.Token);
        var bitSet = ((tokenHash >> bit) & 1) == 1;
        sum += bitSet ? frequency.Weight : -frequency.Weight;
      }

      if (sum > 0) {
        fingerprint |= 1UL << bit;
      }
    }

    return fingerprint;
  }

  private static int HammingDistance(ulong a, ulong b) => BitOperations.PopCount(a ^ b);
}
