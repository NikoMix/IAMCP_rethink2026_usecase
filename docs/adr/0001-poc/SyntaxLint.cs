using System.Text.RegularExpressions;

namespace Adr0001.Poc;

public sealed record Finding(string Rule, int Line, string Snippet);

/// <summary>
/// Static checks for the template syntax subset defined in ADR 0001.
/// <see cref="FindIncompatibilities"/> reports Jinja2 constructs that behave differently in Scriban's Liquid mode;
/// <see cref="FindSubsetViolations"/> reports every tag that the subset grammar does not allow.
/// </summary>
public static partial class SyntaxLint
{
    public const string LoopVariable = "INC-01 loop.* variable (Liquid: forloop.*)";
    public const string FilterCall = "INC-02 filter call syntax name(arg) (Liquid: name: arg)";
    public const string Elif = "INC-03 elif (Liquid: elsif)";
    public const string JinjaComment = "INC-04 {# #} comment (Liquid: {% comment %})";
    public const string Truthiness = "INC-05 presence test: \"\" and [] are truthy in Liquid";
    public const string StandaloneTag = "INC-06 block tag on its own line leaves a blank line";
    public const string StandaloneTagInTable = "INC-06a ... between Markdown table rows (breaks the table)";
    public const string WhitespaceMarker = "INC-07 whitespace-control marker {%- -%}";
    public const string JinjaOnly = "INC-08 Jinja-only statement or operator";
    public const string Guidance = "INC-09 [GUIDANCE: ...] marker in body";
    public const string UnescapedOutput = "INC-10 value output without Markdown escaping";

    private const string Path = @"[a-z_][a-z0-9_]*(?:\.[a-z_][a-z0-9_]*)*";
    private const string Literal = @"(?:""[^""\\]*""|-?\d+(?:\.\d+)?)";

    [GeneratedRegex(@"\{\{(?<body>.*?)\}\}|\{%(?<body>.*?)%\}")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\bloop\.\w+")]
    private static partial Regex LoopRegex();

    [GeneratedRegex(@"\|\s*\w+\s*\(")]
    private static partial Regex FilterCallRegex();

    [GeneratedRegex(@"^\s*-?\s*(?:set|macro|endmacro|include|import|extends|block|endblock|filter|endfilter|with|endwith|call)\b|\bis\s+(?:not\s+)?defined\b|\bnot\s|~|\bif\b.*\belse\b")]
    private static partial Regex JinjaOnlyRegex();

    [GeneratedRegex(@"""[^""]*""")]
    private static partial Regex StringLiteralRegex();

    [GeneratedRegex(@"^\s*(?:if|elsif)\s+" + Path + @"\s*$")]
    private static partial Regex PresenceTestRegex();

    [GeneratedRegex(@"^[ \t]*\{%-?[^%]*-?%\}[ \t]*$")]
    private static partial Regex StandaloneLineRegex();

    // Subset grammar, applied to the text between the delimiters.
    [GeneratedRegex(@"^ (?:(?!loop\.)" + Path + @"|forloop\.index)(?: \| (?:default: " + Literal + @"|join: ""[^""\\]*""))? $")]
    private static partial Regex SubsetOutputRegex();

    [GeneratedRegex(@"^ (?:for [a-z_][a-z0-9_]* in " + Path + @"|(?:if|elsif) " + Path + @"(?: (?:==|!=) " + Literal + @")?|else|endfor|endif|comment|endcomment) $")]
    private static partial Regex SubsetStatementRegex();

    public static IReadOnlyList<Finding> FindIncompatibilities(TemplateSource template)
    {
        var findings = new List<Finding>();
        var lines = template.Body.Replace("\r\n", "\n").Split('\n');
        if (template.Body.Contains("{#", StringComparison.Ordinal))
        {
            findings.Add(new Finding(JinjaComment, 0, "{#"));
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNo = template.BodyStartLine + i;

            if (line.Contains("[GUIDANCE:", StringComparison.Ordinal))
            {
                findings.Add(new Finding(Guidance, lineNo, line.Trim()));
            }

            if (StandaloneLineRegex().IsMatch(line))
            {
                var prev = PreviousContentLine(lines, i, -1);
                var next = PreviousContentLine(lines, i, +1);
                var inTable = prev.StartsWith('|') && next.StartsWith('|');
                findings.Add(new Finding(inTable ? StandaloneTagInTable : StandaloneTag, lineNo, line.Trim()));
            }

            foreach (Match tag in TagRegex().Matches(line))
            {
                var isOutput = tag.Value.StartsWith("{{", StringComparison.Ordinal);
                var body = tag.Groups["body"].Value;
                var snippet = tag.Value;

                if (LoopRegex().IsMatch(body))
                {
                    findings.Add(new Finding(LoopVariable, lineNo, snippet));
                }

                if (isOutput && FilterCallRegex().IsMatch(body))
                {
                    findings.Add(new Finding(FilterCall, lineNo, snippet));
                }

                if (isOutput)
                {
                    findings.Add(new Finding(UnescapedOutput, lineNo, snippet));
                }

                var statement = body.Trim('-', ' ');
                if (!isOutput && statement.StartsWith("elif ", StringComparison.Ordinal))
                {
                    findings.Add(new Finding(Elif, lineNo, snippet));
                }

                if (!isOutput && PresenceTestRegex().IsMatch(statement))
                {
                    findings.Add(new Finding(Truthiness, lineNo, snippet));
                }

                if (body.StartsWith('-') || body.EndsWith('-'))
                {
                    findings.Add(new Finding(WhitespaceMarker, lineNo, snippet));
                }

                if (JinjaOnlyRegex().IsMatch(StringLiteralRegex().Replace(statement, "\"\"")))
                {
                    findings.Add(new Finding(JinjaOnly, lineNo, snippet));
                }
            }
        }

        return findings;
    }

    public static IReadOnlyList<Finding> FindSubsetViolations(TemplateSource template)
    {
        var findings = new List<Finding>();
        var lines = template.Body.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNo = template.BodyStartLine + i;
            foreach (Match tag in TagRegex().Matches(line))
            {
                var body = tag.Groups["body"].Value;
                var ok = tag.Value.StartsWith("{{", StringComparison.Ordinal)
                    ? SubsetOutputRegex().IsMatch(body)
                    : SubsetStatementRegex().IsMatch(body);
                if (!ok)
                {
                    findings.Add(new Finding("SUBSET", lineNo, tag.Value));
                }
            }

            var stripped = TagRegex().Replace(line, string.Empty);
            if (stripped.Contains("{{", StringComparison.Ordinal) || stripped.Contains("{%", StringComparison.Ordinal)
                || stripped.Contains("}}", StringComparison.Ordinal) || stripped.Contains("%}", StringComparison.Ordinal)
                || stripped.Contains("{#", StringComparison.Ordinal))
            {
                findings.Add(new Finding("SUBSET unbalanced or multi-line tag", lineNo, line.Trim()));
            }
        }

        return findings;
    }

    private static string PreviousContentLine(string[] lines, int index, int step)
    {
        for (var j = index + step; j >= 0 && j < lines.Length; j += step)
        {
            if (!StandaloneLineRegex().IsMatch(lines[j]))
            {
                return lines[j].TrimStart();
            }
        }

        return string.Empty;
    }
}
