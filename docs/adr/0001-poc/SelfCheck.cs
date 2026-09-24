using System.Text.Json;
using Markdig;
using Markdig.Syntax;
using MdTable = Markdig.Extensions.Tables.Table;

namespace Adr0001.Poc;

/// <summary>
/// Executable evidence for the renderer rules in ADR 0001. Each check runs twice: with the rule enabled it must pass,
/// and with the rule disabled (negative control) it must fail, proving the check can detect the defect it guards.
/// </summary>
public static class SelfCheck
{
    private sealed record Check(string Name, Func<RenderOptions, bool> Holds, Func<RenderOptions, RenderOptions> Disable);

    public static int Run()
    {
        var checks = new List<Check>
        {
            new("standalone tag rule keeps loop rows inside the Markdown table",
                o => TableRowCount(Render("| A | B |\n| --- | --- |\n{% for r in rows %}\n| {{ r.a }} | {{ r.b }} |\n{% endfor %}\n", """{"rows":[{"a":"1","b":"2"},{"a":"3","b":"4"}]}""", o)) == 3,
                o => o with { StandaloneTagRule = false }),

            new("presence test treats empty string as false",
                o => Render("{% if msa.date %}dated {{ msa.date }}{% else %}undated{% endif %}", """{"msa":{"date":""}}""", o) == "undated",
                o => o with { PresenceTruthiness = false }),

            new("presence test treats empty list as false",
                o => Render("{% if sow.assumptions %}has{% else %}none{% endif %}", """{"sow":{"assumptions":[]}}""", o) == "none",
                o => o with { PresenceTruthiness = false }),

            new("value containing | does not add a table column",
                o => TableColumnCount(Render("| A | B |\n| --- | --- |\n| {{ x }} | y |\n", """{"x":"cut | over"}""", o)) == 2,
                o => o with { EscapeMarkdown = false }),

            new("value containing ** is not rendered bold",
                o => !HasEmphasis(Render("{{ x }}", """{"x":"**not bold**"}""", o)),
                o => o with { EscapeMarkdown = false }),

            new("template Markdown is not escaped (only values are)",
                o => HasEmphasis(Render("**{{ x }}**", """{"x":"Contoso"}""", o)),
                o => o),

            new("Jinja loop.index fails the render instead of printing nothing",
                o => Throws(() => Render("{% for x in xs %}{{ loop.index }}{% endfor %}", """{"xs":["a"]}""", o)),
                o => o with { StrictVariables = false }),

            new("absent top-level catalog object renders as empty under strict mode",
                o => Render("[{{ msa.reference }}]", "{}", o) == "[]",
                o => o),

            new("missing required field rejects the document",
                o => Throws(() => SubsetRenderer.Render(new TemplateSource("t", "t", ["customer.name"], "{{ customer.name }}", 1), Json("""{"customer":{"name":" "}}"""), o)),
                o => o with { CheckRequiredFields = false }),

            new("guidance markers are removed from output",
                o => !Render("Text\n> [GUIDANCE: remove me]\nMore [GUIDANCE: inline]\n", "{}", o).Contains("GUIDANCE", StringComparison.Ordinal),
                o => o with { StripGuidance = false }),
        };

        var failures = 0;
        var controlsRun = 0;
        foreach (var check in checks)
        {
            var defaults = new RenderOptions();
            var holds = Safe(() => check.Holds(defaults));
            var disabled = check.Disable(defaults);
            string control;
            if (disabled == defaults)
            {
                control = "no control (positive property)";
            }
            else
            {
                controlsRun++;
                var controlHolds = Safe(() => check.Holds(disabled));
                control = controlHolds ? "CONTROL DID NOT FAIL" : "control failed as expected";
                if (controlHolds)
                {
                    failures++;
                }
            }

            if (!holds)
            {
                failures++;
            }

            Console.WriteLine($"{(holds ? "PASS" : "FAIL")}  {check.Name}  [{control}]");
        }

        Console.WriteLine($"selfcheck: checks={checks.Count} negative-controls={controlsRun} failures={failures}");
        failures += LintRulesFire();
        return failures == 0 ? 0 : 1;
    }

    /// <summary>A zero count from a lint rule means nothing unless the rule demonstrably fires on known input.</summary>
    private static int LintRulesFire()
    {
        const string jinja = """
            | A |
            | --- |
            {% for x in xs %}
            | {{ loop.index }} {{ x | default("n/a") }} |
            {% endfor %}
            {% if a %}A{% elif b %}B{% endif %}
            {# comment #}
            {%- if c -%}C{% endif %}
            {% set y = 1 %}{% if d is defined %}D{% endif %}
            [GUIDANCE: remove]
            """;
        const string subset = """
            | A |
            | --- |
            {% for x in xs %}
            | {{ forloop.index }} {{ x | default: "n/a" }} {{ x.list | join: ", " }} |
            {% endfor %}
            {% if a %}A{% elsif b == "b" %}B{% else %}C{% endif %}{% comment %}note{% endcomment %}
            """;

        var jinjaTemplate = new TemplateSource("jinja", "jinja", [], jinja, 1);
        var found = SyntaxLint.FindIncompatibilities(jinjaTemplate).Select(f => f.Rule).ToHashSet();
        string[] expected =
        [
            SyntaxLint.LoopVariable, SyntaxLint.FilterCall, SyntaxLint.Elif, SyntaxLint.JinjaComment, SyntaxLint.Truthiness,
            SyntaxLint.StandaloneTag, SyntaxLint.StandaloneTagInTable, SyntaxLint.WhitespaceMarker, SyntaxLint.JinjaOnly,
            SyntaxLint.Guidance, SyntaxLint.UnescapedOutput,
        ];

        var failures = 0;
        foreach (var rule in expected)
        {
            var fires = found.Contains(rule);
            failures += fires ? 0 : 1;
            Console.WriteLine($"{(fires ? "PASS" : "FAIL")}  lint rule fires on known input: {rule}");
        }

        var jinjaViolations = SyntaxLint.FindSubsetViolations(jinjaTemplate).Count;
        var subsetTemplate = new TemplateSource("subset", "subset", [], subset, 1);
        var subsetViolations = SyntaxLint.FindSubsetViolations(subsetTemplate).Count;
        var subsetJinja = SyntaxLint.FindIncompatibilities(subsetTemplate)
            .Count(f => f.Rule is SyntaxLint.LoopVariable or SyntaxLint.FilterCall or SyntaxLint.Elif or SyntaxLint.JinjaComment or SyntaxLint.WhitespaceMarker or SyntaxLint.JinjaOnly);
        var grammarOk = jinjaViolations == 7 && subsetViolations == 0 && subsetJinja == 0;
        failures += grammarOk ? 0 : 1;
        Console.WriteLine($"{(grammarOk ? "PASS" : "FAIL")}  subset grammar: jinja-sample violations={jinjaViolations} (expect 7), subset-sample violations={subsetViolations} (expect 0), subset-sample jinja findings={subsetJinja} (expect 0)");
        Console.WriteLine($"selfcheck lint: rules={expected.Length} failures={failures}");
        return failures;
    }

    private static string Render(string body, string json, RenderOptions options) =>
        SubsetRenderer.Render(new TemplateSource("selfcheck", "selfcheck", [], body, 1), Json(json), options);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static int TableRowCount(string markdown) =>
        Markdown.Parse(markdown, DocxWriter.Pipeline).Descendants<MdTable>().FirstOrDefault()?.Count ?? 0;

    private static int TableColumnCount(string markdown) =>
        Markdown.Parse(markdown, DocxWriter.Pipeline).Descendants<MdTable>().FirstOrDefault()?.OfType<Markdig.Extensions.Tables.TableRow>().Max(r => r.Count) ?? 0;

    private static bool HasEmphasis(string markdown) =>
        Markdown.Parse(markdown, DocxWriter.Pipeline).Descendants<Markdig.Syntax.Inlines.EmphasisInline>().Any();

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (TemplateRenderException)
        {
            return true;
        }
    }

    private static bool Safe(Func<bool> predicate)
    {
        try
        {
            return predicate();
        }
        catch (TemplateRenderException)
        {
            return false;
        }
    }
}
