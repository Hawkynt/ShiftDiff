namespace Showcase.Tools;

public static class Format
{
    public static string Compact(string value) => value.Trim().Replace("  ", " ");
}
