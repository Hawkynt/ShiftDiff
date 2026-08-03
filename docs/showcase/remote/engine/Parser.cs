namespace Showcase;

public sealed class Parser
{
    public Result Parse(string source)
    {
        var tokens = Tokenize(source);
        Validate(tokens);
        return new Result(tokens, $"Accepted {tokens.Length} tokens");
    }

    private static string[] Tokenize(string source) =>
        source.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static void Validate(IReadOnlyCollection<string> tokens)
    {
        if (tokens.Count == 0)
            throw new InvalidOperationException("No tokens were produced.");
    }
}
