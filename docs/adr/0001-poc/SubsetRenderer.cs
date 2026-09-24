using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Adr0001.Poc;

/// <summary>Switches exist only so the self-check can prove each rule is load-bearing (negative controls).</summary>
public sealed record RenderOptions
{
    public bool StandaloneTagRule { get; init; } = true;
    public bool PresenceTruthiness { get; init; } = true;
    public bool EscapeMarkdown { get; init; } = true;
    public bool StrictVariables { get; init; } = true;
    public bool StripGuidance { get; init; } = true;
    public bool CheckRequiredFields { get; init; } = true;
}

public sealed class TemplateRenderException(string message) : Exception(message);

/// <summary>Renders a template body that follows the ADR 0001 syntax subset to Markdown.</summary>
public static partial class SubsetRenderer
{
    /// <summary>Top-level objects of the shared field catalog (templates/README.md) plus document-specific roots.</summary>
    public static readonly IReadOnlyList<string> TopLevelObjects =
        ["document", "supplier", "customer", "project", "pricing", "legal", "msa", "sow", "cr", "rfi", "rfp"];

    [GeneratedRegex(@"^[ \t]*(\{%[^%\n]*%\})[ \t]*\n", RegexOptions.Multiline)]
    private static partial Regex StandaloneTagLineRegex();

    [GeneratedRegex(@"^[ \t]*(?:>[ \t]*)?\[GUIDANCE:[^\]\n]*\][ \t]*\n", RegexOptions.Multiline)]
    private static partial Regex GuidanceLineRegex();

    [GeneratedRegex(@"[ \t]*\[GUIDANCE:[^\]\n]*\]")]
    private static partial Regex GuidanceInlineRegex();

    [GeneratedRegex(@"^(\s*)(\d+)([.)])", RegexOptions.Multiline)]
    private static partial Regex OrderedListMarkerRegex();

    [GeneratedRegex(@"^(\s*)([-+=])", RegexOptions.Multiline)]
    private static partial Regex BlockMarkerRegex();

    public static string Render(TemplateSource template, JsonElement data, RenderOptions? options = null)
    {
        options ??= new RenderOptions();

        if (options.CheckRequiredFields)
        {
            var missing = template.RequiredFields.Where(path => IsMissing(data, path)).ToList();
            if (missing.Count > 0)
            {
                throw new TemplateRenderException($"{template.Name}: missing required fields: {string.Join(", ", missing)}");
            }
        }

        // Line endings are normalised first; the standalone tag rule matches LF only.
        var body = template.Body.Replace("\r\n", "\n");
        body = options.StandaloneTagRule ? ApplyStandaloneTagRule(body) : body;
        var parsed = Template.ParseLiquid(body, template.Name);
        if (parsed.HasErrors)
        {
            throw new TemplateRenderException($"{template.Name}: {string.Join("; ", parsed.Messages)}");
        }

        var globals = new ScriptObject();
        foreach (var name in TopLevelObjects)
        {
            globals[name] = null;
        }

        foreach (var property in data.EnumerateObject())
        {
            globals[property.Name] = ToScriptValue(property.Value);
        }

        var context = new SubsetTemplateContext(options)
        {
            StrictVariables = options.StrictVariables,
            LoopLimit = 1000,
            RecursiveLimit = 20,
        };
        context.PushGlobal(globals);

        string markdown;
        try
        {
            markdown = parsed.Render(context);
        }
        catch (Scriban.Syntax.ScriptRuntimeException ex)
        {
            throw new TemplateRenderException($"{template.Name}: {ex.Message}");
        }

        if (options.StripGuidance)
        {
            markdown = GuidanceLineRegex().Replace(markdown, string.Empty);
            markdown = GuidanceInlineRegex().Replace(markdown, string.Empty);
        }

        return markdown;
    }

    /// <summary>
    /// A line that contains nothing but one block tag is removed together with its line break,
    /// so loops between table rows or list items do not leave blank lines behind.
    /// </summary>
    public static string ApplyStandaloneTagRule(string body) => StandaloneTagLineRegex().Replace(body, "$1");

    public static string EscapeMarkdown(string value)
    {
        var normalised = value.Replace("\r\n", "\n");
        var sb = new StringBuilder(normalised.Length + 8);
        foreach (var ch in normalised)
        {
            if (ch is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '|' or '#' or '&' or '~')
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        var escaped = OrderedListMarkerRegex().Replace(sb.ToString(), "$1$2\\$3");
        return BlockMarkerRegex().Replace(escaped, "$1\\$2");
    }

    public static bool IsMissing(JsonElement data, string path)
    {
        var current = data;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return true;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(current.GetString()),
            JsonValueKind.Array => current.GetArrayLength() == 0,
            _ => false,
        };
    }

    private static object? ToScriptValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ToScriptObject(element),
        JsonValueKind.Array => ToScriptArray(element),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    private static ScriptObject ToScriptObject(JsonElement element)
    {
        var obj = new ScriptObject();
        foreach (var property in element.EnumerateObject())
        {
            obj[property.Name] = ToScriptValue(property.Value);
        }

        return obj;
    }

    private static ScriptArray ToScriptArray(JsonElement element)
    {
        var array = new ScriptArray();
        foreach (var item in element.EnumerateArray())
        {
            array.Add(ToScriptValue(item));
        }

        return array;
    }

    private sealed class SubsetTemplateContext(RenderOptions options) : LiquidTemplateContext
    {
        public override bool ToBool(SourceSpan span, object? value)
        {
            if (!options.PresenceTruthiness)
            {
                return base.ToBool(span, value);
            }

            return value switch
            {
                null => false,
                string s => !string.IsNullOrWhiteSpace(s),
                ICollection c => c.Count > 0,
                _ => base.ToBool(span, value),
            };
        }

        public override TemplateContext Write(SourceSpan span, object? textAsObject)
        {
            if (options.EscapeMarkdown && textAsObject is string s)
            {
                return base.Write(span, EscapeMarkdown(s));
            }

            return base.Write(span, textAsObject);
        }
    }
}
