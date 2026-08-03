namespace Showcase;

public sealed class Parser
{
    public Result Parse(string source)
    {
        Validate(source);
        var tokens = Tokenize(source);
        return new Result(tokens, Describe(tokens));
    }

    private static void Validate(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source is required.", nameof(source));
    }

    private static string Describe(IReadOnlyList<string> tokens)
    {
        return $"Parsed {tokens.Count} tokens";
    }

    private static string[] Tokenize(string source) => source.Split(' ');
}
