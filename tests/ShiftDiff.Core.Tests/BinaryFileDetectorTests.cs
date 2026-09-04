using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class BinaryFileDetectorTests {
  [Fact]
  public void IsBinary_ContentWithNulByteWithinSniffWindow_ReturnsTrue() {
    var content = new byte[100];
    content[50] = 0x00;
    Assert.True(BinaryFileDetector.IsBinary(content));
  }

  [Fact]
  public void IsBinary_PlainTextContent_ReturnsFalse() {
    var content = System.Text.Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog");
    Assert.False(BinaryFileDetector.IsBinary(content));
  }

  [Fact]
  public void IsBinary_NulByteBeyondSniffWindow_ReturnsFalse() {
    var content = new byte[8100];
    Array.Fill(content, (byte)'a');
    content[8050] = 0x00;
    Assert.False(BinaryFileDetector.IsBinary(content));
  }

  [Fact]
  public void IsBinary_EmptyContent_ReturnsFalse() {
    Assert.False(BinaryFileDetector.IsBinary(Array.Empty<byte>()));
  }

  [Fact]
  public void AreEqual_IdenticalByteArrays_ReturnsTrue() {
    var a = new byte[] { 1, 2, 3, 4 };
    var b = new byte[] { 1, 2, 3, 4 };
    Assert.True(BinaryFileDetector.AreEqual(a, b));
  }

  [Fact]
  public void AreEqual_DifferentContentSameLength_ReturnsFalse() {
    var a = new byte[] { 1, 2, 3, 4 };
    var b = new byte[] { 1, 2, 3, 5 };
    Assert.False(BinaryFileDetector.AreEqual(a, b));
  }

  [Fact]
  public void AreEqual_DifferentLength_ReturnsFalse() {
    var a = new byte[] { 1, 2, 3, 4 };
    var b = new byte[] { 1, 2, 3 };
    Assert.False(BinaryFileDetector.AreEqual(a, b));
  }

  [Fact]
  public void Describe_EmptyContent_SizeIsZeroAndHashIsKnownSha256Constant() {
    var info = BinaryFileDetector.Describe(Array.Empty<byte>());
    Assert.Equal(0, info.Size);
    Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", info.Hash);
  }

  [Fact]
  public void Describe_ModifiedAtOmitted_DefaultsToNull() {
    var info = BinaryFileDetector.Describe(new byte[] { 1, 2, 3 });
    Assert.Null(info.ModifiedAt);
  }

  [Fact]
  public void Describe_NonEmptyContent_SizeMatchesLength() {
    var content = new byte[] { 1, 2, 3, 4, 5 };
    var first = BinaryFileDetector.Describe(content);
    var second = BinaryFileDetector.Describe(content);
    Assert.Equal(5, first.Size);
    Assert.False(string.IsNullOrEmpty(first.Hash));
    Assert.Equal(first.Hash, second.Hash);
  }
}
