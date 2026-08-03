using ShiftDiff.Cli;
using ShiftDiff.Core;
using Xunit;

namespace ShiftDiff.Cli.Tests;

public class CliOptionsParserTests
{
    [Fact]
    public void Parse_NoArguments_SelectsHelp()
    {
        var result = CliOptionsParser.Parse([]);

        Assert.True(result.IsValid);
        Assert.Equal(CliCommand.Help, result.Options!.Command);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void Parse_HelpFlags_SelectHelp(string argument)
    {
        Assert.Equal(CliCommand.Help, CliOptionsParser.Parse([argument]).Options!.Command);
    }

    [Fact]
    public void Parse_CompareSubcommand_TakesTwoOperands()
    {
        var options = CliOptionsParser.Parse(["compare", "a.cs", "b.cs"]).Options!;

        Assert.Equal(CliCommand.Compare, options.Command);
        Assert.Equal(["a.cs", "b.cs"], options.Operands);
    }

    [Theory]
    [InlineData(2, CliCommand.Compare)]
    [InlineData(3, CliCommand.Compare3)]
    [InlineData(4, CliCommand.Compare4)]
    public void Parse_BarePositionalFiles_InfersCommandFromCount(int count, CliCommand expected)
    {
        var args = Enumerable.Range(0, count).Select(i => $"f{i}.cs").ToArray();

        Assert.Equal(expected, CliOptionsParser.Parse(args).Options!.Command);
    }

    [Fact]
    public void Parse_OneBareFile_IsInvalidInput()
    {
        var result = CliOptionsParser.Parse(["only-one"]);

        Assert.False(result.IsValid);
        Assert.Contains("2 to 4", result.Error);
    }

    [Fact]
    public void Parse_CompareWithWrongOperandCount_ReportsTheExpectedCount()
    {
        var result = CliOptionsParser.Parse(["compare3", "a", "b"]);

        Assert.False(result.IsValid);
        Assert.Contains("compare3 expects 3", result.Error);
    }

    [Fact]
    public void Parse_LegacyPatchAndSource_MapsToApplyPatchWithSourceFirst()
    {
        var options = CliOptionsParser.Parse(["--patch", "p.diff", "--source", "s.cs"]).Options!;

        Assert.Equal(CliCommand.ApplyPatch, options.Command);
        Assert.Equal(["s.cs", "p.diff"], options.Operands);
    }

    [Fact]
    public void Parse_PatchWithoutSource_IsInvalidInput()
    {
        var result = CliOptionsParser.Parse(["--patch", "p.diff"]);

        Assert.False(result.IsValid);
        Assert.Contains("--source", result.Error);
    }

    [Fact]
    public void Parse_Json_SelectsJsonFormat()
    {
        Assert.Equal(OutputFormat.Json, CliOptionsParser.Parse(["compare", "a", "b", "--json"]).Options!.Format);
    }

    [Theory]
    [InlineData("semantic", OutputFormat.Semantic)]
    [InlineData("unified", OutputFormat.Unified)]
    [InlineData("git", OutputFormat.Git)]
    [InlineData("svn", OutputFormat.Svn)]
    [InlineData("json", OutputFormat.Json)]
    public void Parse_FormatOption_SelectsFormat(string text, OutputFormat expected)
    {
        Assert.Equal(expected, CliOptionsParser.Parse(["compare", "a", "b", "--format", text]).Options!.Format);
    }

    [Fact]
    public void Parse_UnknownFormat_IsInvalidInput()
    {
        var result = CliOptionsParser.Parse(["compare", "a", "b", "--format", "yaml"]);

        Assert.False(result.IsValid);
        Assert.Contains("yaml", result.Error);
    }

    [Fact]
    public void Parse_EqualsSyntax_IsEquivalentToSpaceSyntax()
    {
        var options = CliOptionsParser.Parse(["compare", "a", "b", "--mode=aggressive"]).Options!;

        Assert.Equal(DetectionMode.Aggressive, options.Detection);
    }

    [Fact]
    public void Parse_WhitespaceAndCaseOptions_AreApplied()
    {
        var options = CliOptionsParser.Parse(["compare", "a", "b", "--ignore-case", "--ignore-whitespace", "trim"]).Options!;

        Assert.True(options.IgnoreCase);
        Assert.Equal(WhitespaceMode.Trim, options.Whitespace);
    }

    [Fact]
    public void Parse_UnknownWhitespaceMode_IsInvalidInput()
    {
        Assert.False(CliOptionsParser.Parse(["compare", "a", "b", "--ignore-whitespace", "sideways"]).IsValid);
    }

    [Theory]
    [InlineData("exact", PatchApplyMode.Exact)]
    [InlineData("fuzzy", PatchApplyMode.Fuzzy)]
    [InlineData("semantic", PatchApplyMode.Semantic)]
    public void Parse_PatchMode_SelectsApplyStrategy(string text, PatchApplyMode expected)
    {
        var options = CliOptionsParser.Parse(["apply-patch", "s", "p", "--patch-mode", text]).Options!;

        Assert.Equal(expected, options.PatchMode);
    }

    [Fact]
    public void Parse_EmojiFlags_ToggleMarkerStyle()
    {
        Assert.True(CliOptionsParser.Parse(["compare", "a", "b", "--emoji"]).Options!.UseEmoji);
        Assert.False(CliOptionsParser.Parse(["compare", "a", "b", "--emoji", "--no-emoji"]).Options!.UseEmoji);
        Assert.False(CliOptionsParser.Parse(["compare", "a", "b"]).Options!.UseEmoji);
    }

    [Fact]
    public void Parse_ContextOption_SetsContextLines()
    {
        Assert.Equal(0, CliOptionsParser.Parse(["compare", "a", "b", "--context", "0"]).Options!.ContextLines);
        Assert.Equal(3, CliOptionsParser.Parse(["compare", "a", "b"]).Options!.ContextLines);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("many")]
    public void Parse_InvalidContext_IsInvalidInput(string text)
    {
        Assert.False(CliOptionsParser.Parse(["compare", "a", "b", "--context", text]).IsValid);
    }

    [Fact]
    public void Parse_UnknownOption_IsInvalidInput()
    {
        var result = CliOptionsParser.Parse(["compare", "a", "b", "--wat"]);

        Assert.False(result.IsValid);
        Assert.Contains("--wat", result.Error);
    }

    [Fact]
    public void Parse_GitCommand_PassesRemainingArgumentsThroughUntouched()
    {
        var options = CliOptionsParser.Parse(["git", "diff", "HEAD~1", "--stat"]).Options!;

        Assert.Equal(CliCommand.Git, options.Command);
        Assert.Equal(["diff", "HEAD~1", "--stat"], options.Operands);
    }

    [Fact]
    public void Parse_SvnCommand_PassesRemainingArgumentsThroughUntouched()
    {
        var options = CliOptionsParser.Parse(["svn", "diff", "-r", "1200:1250"]).Options!;

        Assert.Equal(CliCommand.Svn, options.Command);
        Assert.Equal(["diff", "-r", "1200:1250"], options.Operands);
    }

    [Fact]
    public void Parse_OutAndForce_AreCaptured()
    {
        var options = CliOptionsParser.Parse(["export-patch", "a", "b", "--out", "p.diff", "--force"]).Options!;

        Assert.Equal("p.diff", options.OutPath);
        Assert.True(options.Force);
    }

    [Fact]
    public void Parse_Version_SelectsVersionCommand()
    {
        Assert.Equal(CliCommand.Version, CliOptionsParser.Parse(["--version"]).Options!.Command);
    }
}
