using System.Text;
using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class TextFileLoaderTests {
  [Fact]
  public void Load_Utf8Bom_DetectsUtf8AndStripsBom() {
    var bom = new byte[] { 0xEF, 0xBB, 0xBF };
    var content = bom.Concat(Encoding.UTF8.GetBytes("hello\nworld")).ToArray();

    var result = TextFileLoader.Load(content);

    Assert.Equal("utf-8", result.Encoding.WebName);
    Assert.Equal(new[] { "hello", "world" }, result.Lines);
  }

  [Fact]
  public void Load_Utf16LeBom_DetectsUtf16Le() {
    var bom = new byte[] { 0xFF, 0xFE };
    var content = bom.Concat(Encoding.Unicode.GetBytes("hi")).ToArray();

    var result = TextFileLoader.Load(content);

    Assert.Equal("utf-16", result.Encoding.WebName);
    Assert.Equal(new[] { "hi" }, result.Lines);
  }

  [Fact]
  public void Load_Utf32LeBom_DetectsUtf32Le() {
    var bom = new byte[] { 0xFF, 0xFE, 0x00, 0x00 };
    var content = bom.Concat(new UTF32Encoding(bigEndian: false, byteOrderMark: false).GetBytes("hi")).ToArray();

    var result = TextFileLoader.Load(content);

    Assert.Equal("utf-32", result.Encoding.WebName);
    Assert.Equal(new[] { "hi" }, result.Lines);
  }

  [Fact]
  public void Load_Utf32BeBom_DetectsUtf32Be() {
    var bom = new byte[] { 0x00, 0x00, 0xFE, 0xFF };
    var content = bom.Concat(new UTF32Encoding(bigEndian: true, byteOrderMark: false).GetBytes("hi")).ToArray();

    var result = TextFileLoader.Load(content);

    Assert.Equal("utf-32BE", result.Encoding.WebName);
    Assert.Equal(new[] { "hi" }, result.Lines);
  }

  [Fact]
  public void Load_Utf16BeBom_DetectsUtf16Be() {
    var bom = new byte[] { 0xFE, 0xFF };
    var content = bom.Concat(Encoding.BigEndianUnicode.GetBytes("hi")).ToArray();

    var result = TextFileLoader.Load(content);

    Assert.Equal("utf-16BE", result.Encoding.WebName);
    Assert.Equal(new[] { "hi" }, result.Lines);
  }

  [Fact]
  public void Load_BareCrLineEndings_ReportsCr() {
    var content = Encoding.UTF8.GetBytes("a\rb\rc");

    var result = TextFileLoader.Load(content);

    Assert.Equal(new[] { "a", "b", "c" }, result.Lines);
    Assert.Equal(LineEnding.Cr, result.OriginalEnding);
  }

  [Fact]
  public void Load_NoBom_DefaultsToUtf8() {
    var content = Encoding.UTF8.GetBytes("plain text");

    var result = TextFileLoader.Load(content);

    Assert.Equal("utf-8", result.Encoding.WebName);
    Assert.Equal(new[] { "plain text" }, result.Lines);
  }

  [Fact]
  public void Load_CrLfLineEndings_SplitsCorrectlyAndReportsCrLf() {
    var content = Encoding.UTF8.GetBytes("a\r\nb\r\nc");

    var result = TextFileLoader.Load(content);

    Assert.Equal(new[] { "a", "b", "c" }, result.Lines);
    Assert.Equal(LineEnding.CrLf, result.OriginalEnding);
  }

  [Fact]
  public void Load_LfLineEndings_ReportsLf() {
    var content = Encoding.UTF8.GetBytes("a\nb\nc");

    var result = TextFileLoader.Load(content);

    Assert.Equal(new[] { "a", "b", "c" }, result.Lines);
    Assert.Equal(LineEnding.Lf, result.OriginalEnding);
  }

  [Fact]
  public void Load_MixedLineEndings_ReportsMixed() {
    var content = Encoding.UTF8.GetBytes("a\r\nb\nc");

    var result = TextFileLoader.Load(content);

    Assert.Equal(LineEnding.Mixed, result.OriginalEnding);
  }

  [Fact]
  public void Load_EmptyContent_DefaultsToUtf8WithSingleEmptyLine() {
    var result = TextFileLoader.Load(Array.Empty<byte>());

    Assert.Equal("utf-8", result.Encoding.WebName);
    Assert.Equal(new[] { "" }, result.Lines);
    Assert.Equal(LineEnding.Lf, result.OriginalEnding);
  }
}
