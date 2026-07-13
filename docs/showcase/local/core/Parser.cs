namespace Showcase;

public sealed class Parser
{
    private static string Describe(IReadOnlyList<string> tokens)
    {
        return tokens.Count == 1
            ? "Parsed one token"
            : $"Parsed {tokens.Count} tokens";
    }

    public Result Parse(string source)
    {
        Validate(source);
        var tokens = Tokenize(source);
        return new Result(tokens, Describe(tokens));
    }

    private static string[] Tokenize(string source) =>
        source.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static void Validate(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
    }
}
