using System.Security.Cryptography;

namespace ShiftDiff.Core;

public sealed record BinaryFileInfo(long Size, string Hash, DateTimeOffset? ModifiedAt);

public static class BinaryFileDetector {
  // Git's own heuristic (buffer_is_binary): a NUL byte anywhere in the
  // first 8000 bytes marks content as binary — no legitimate text
  // encoding embeds NUL, so this is a reliable, dependency-free split.
  private const int SniffLength = 8000;

  public static bool IsBinary(byte[] content) {
    var length = Math.Min(content.Length, SniffLength);

    for (var i = 0; i < length; i++) {
      if (content[i] == 0) {
        return true;
      }
    }

    return false;
  }

  public static bool AreEqual(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);

  public static BinaryFileInfo Describe(byte[] content, DateTimeOffset? modifiedAt = null) =>
      new(content.LongLength, ComputeHash(content), modifiedAt);

  private static string ComputeHash(byte[] content) =>
      Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
