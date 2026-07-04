using System.Text;
using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Core.Tests;

public class TextFileLoaderTests
{
    [Fact]
    public void Load_Utf8Bom_DetectsUtf8AndStripsBom()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = bom.Concat(Encoding.UTF8.GetBytes("hello\nworld")).ToArray();

        var result = TextFileLoader.Load(content);

        Assert.Equal("utf-8", result.Encoding.WebName);
        Assert.Equal(new[] { "hello", "world" }, result.Lines);
    }

    [Fact]
    public void Load_Utf16LeBom_DetectsUtf16Le()
    {
        var bom = new byte[] { 0xFF, 0xFE };
        var content = bom.Concat(Encoding.Unicode.GetBytes("hi")).ToArray();

        var result = TextFileLoader.Load(content);

        Assert.Equal("utf-16", result.Encoding.WebName);
        Assert.Equal(new[] { "hi" }, result.Lines);
    }

    [Fact]
    public void Load_NoBom_DefaultsToUtf8()
    {
        var content = Encoding.UTF8.GetBytes("plain text");

        var result = TextFileLoader.Load(content);

        Assert.Equal("utf-8", result.Encoding.WebName);
        Assert.Equal(new[] { "plain text" }, result.Lines);
    }

    [Fact]
    public void Load_CrLfLineEndings_SplitsCorrectlyAndReportsCrLf()
    {
        var content = Encoding.UTF8.GetBytes("a\r\nb\r\nc");

        var result = TextFileLoader.Load(content);

        Assert.Equal(new[] { "a", "b", "c" }, result.Lines);
        Assert.Equal(LineEnding.CrLf, result.OriginalEnding);
    }

    [Fact]
    public void Load_LfLineEndings_ReportsLf()
    {
        var content = Encoding.UTF8.GetBytes("a\nb\nc");

        var result = TextFileLoader.Load(content);

        Assert.Equal(new[] { "a", "b", "c" }, result.Lines);
        Assert.Equal(LineEnding.Lf, result.OriginalEnding);
    }

    [Fact]
    public void Load_MixedLineEndings_ReportsMixed()
    {
        var content = Encoding.UTF8.GetBytes("a\r\nb\nc");

        var result = TextFileLoader.Load(content);

        Assert.Equal(LineEnding.Mixed, result.OriginalEnding);
    }
}
