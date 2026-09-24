using System.Text.RegularExpressions;

namespace Adr0001.Poc;

/// <summary>
/// Mechanical rewrite of the Jinja2-style constructs found in PR #45's templates into the ADR 0001 subset.
/// Only used by the PoC to demonstrate the adaptation; template owners apply the same rewrite in the source files.
/// </summary>
public static partial class JinjaToSubset
{
    [GeneratedRegex(@"\bloop\.(index|first|last)\b")]
    private static partial Regex LoopRegex();

    [GeneratedRegex(@"\|\s*(default|join)\s*\(\s*(""[^""]*""|-?\d+(?:\.\d+)?)\s*\)")]
    private static partial Regex FilterCallRegex();

    [GeneratedRegex(@"\{%(-?)\s*elif\b")]
    private static partial Regex ElifRegex();

    public static string Migrate(string body)
    {
        body = LoopRegex().Replace(body, "forloop.$1");
        body = FilterCallRegex().Replace(body, "| $1: $2");
        body = ElifRegex().Replace(body, "{%$1 elsif");
        return body;
    }
}
