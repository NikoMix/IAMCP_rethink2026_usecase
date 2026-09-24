using System.Globalization;
using System.Text;
using System.Text.Json;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace ProposalGenerator.Rendering.Templates;

/// <summary>A parsed template body that passed the subset check.</summary>
internal sealed record CompiledTemplate(TemplateDefinition Definition, Template Template, StandaloneTagTrimmer.TrimResult Trimmed);

/// <summary>Renders a template body to Markdown with Scriban in Liquid mode.</summary>
internal static class ScribanTemplateRenderer
{
    /// <summary>
    /// Parses and checks the template body. Throws <see cref="TemplateSyntaxException"/> with every
    /// parse error or subset violation, mapped to lines of the template file.
    /// </summary>
    public static CompiledTemplate Compile(TemplateDefinition definition)
    {
        var trimmed = StandaloneTagTrimmer.Trim(definition.Body);
        var template = Template.ParseLiquid(trimmed.Text, definition.SourceName);

        var errors = new List<TemplateSyntaxError>();
        foreach (var message in template.Messages.Where(m => m.Type == ParserMessageType.Error))
        {
            errors.Add(ToError(definition, trimmed, message.Span.Start.Line, message.Span.Start.Column, message.Message));
        }

        if (!template.HasErrors && template.Page is not null)
        {
            foreach (var (line, column, message) in TemplateSubsetValidator.Validate(template.Page))
            {
                errors.Add(ToError(definition, trimmed, line, column, message));
            }
        }

        if (errors.Count > 0)
        {
            throw new TemplateSyntaxException(
                definition.TemplateId,
                errors.OrderBy(e => e.Line).ThenBy(e => e.Column).ToArray());
        }

        return new CompiledTemplate(definition, template, trimmed);
    }

    /// <summary>Renders the compiled template against <paramref name="data"/>.</summary>
    public static string Render(CompiledTemplate compiled, JsonElement data, CancellationToken cancellationToken)
    {
        var (definition, template, trimmed) = compiled;
        var globals = (ScriptObject)JsonToScript.Convert(data)!;
        var context = new MarkdownTemplateContext(cancellationToken)
        {
            // Optional fields may be absent at any depth: `{% if msa.reference %}` is false and
            // `{{ msa.reference }}` is empty when `msa` does not exist. Required fields are checked before rendering.
            StrictVariables = false,
            EnableRelaxedMemberAccess = true,
            EnableRelaxedTargetAccess = true,
            MemberRenamer = member => member.Name,
        };
        context.PushGlobal(globals);

        try
        {
            return template.Render(context);
        }
        catch (ScriptRuntimeException ex)
        {
            throw new TemplateEvaluationException(
                definition.TemplateId,
                ToError(definition, trimmed, ex.Span.Start.Line, ex.Span.Start.Column, ex.OriginalMessage),
                ex);
        }
    }

    private static TemplateSyntaxError ToError(
        TemplateDefinition definition,
        StandaloneTagTrimmer.TrimResult trimmed,
        int line,
        int column,
        string message)
    {
        var (originalLine, originalColumn) = trimmed.ToOriginal(line, column);
        return new TemplateSyntaxError(definition.BodyStartLine + originalLine, originalColumn + 1, message);
    }

    /// <summary>
    /// Converts every value written into the template to Markdown-safe text, so data cannot inject
    /// Markdown structure (tables, emphasis, headings) into the document.
    /// </summary>
    /// <remarks>
    /// This is the single place where values become text. Locale-aware formatting of numbers,
    /// amounts and dates (#27) belongs here.
    /// </remarks>
    private sealed class MarkdownTemplateContext(CancellationToken cancellationToken) : TemplateContext(new ScriptObject())
    {
        public override string ObjectToString(object? value, bool nested = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return value switch
            {
                null => string.Empty,
                string text => MarkdownText.Escape(text),
                bool flag => flag ? "true" : "false",
                decimal number => MarkdownText.Escape(number.ToString(CultureInfo.InvariantCulture)),
                long number => MarkdownText.Escape(number.ToString(CultureInfo.InvariantCulture)),
                double number => MarkdownText.Escape(number.ToString("R", CultureInfo.InvariantCulture)),
                ScriptObject or ScriptArray => throw new ScriptRuntimeException(
                    CurrentSpan,
                    $"The value is a {(value is ScriptArray ? "list" : "object")}, not text. Output one of its fields, or loop over the list."),
                _ => throw new ScriptRuntimeException(CurrentSpan, $"Values of type '{value.GetType().Name}' cannot be written."),
            };
        }
    }
}

/// <summary>Converts <see cref="JsonElement"/> trees to Scriban runtime objects.</summary>
internal static class JsonToScript
{
    public static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ToObject(element),
        JsonValueKind.Array => ToArray(element),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    private static ScriptObject ToObject(JsonElement element)
    {
        var result = new ScriptObject();
        foreach (var property in element.EnumerateObject())
        {
            result.SetValue(property.Name, Convert(property.Value), readOnly: true);
        }

        return result;
    }

    private static ScriptArray ToArray(JsonElement element)
    {
        var result = new ScriptArray();
        foreach (var item in element.EnumerateArray())
        {
            result.Add(Convert(item));
        }

        return result;
    }
}

/// <summary>Markdown escaping for substituted values.</summary>
internal static class MarkdownText
{
    /// <summary>
    /// Backslash-escapes every ASCII punctuation character (CommonMark allows this for all of them),
    /// replaces line breaks with spaces, drops other control characters (they are invalid in
    /// WordprocessingML), and trims surrounding whitespace, so a value always renders as literal
    /// inline text, including inside table cells.
    /// </summary>
    public static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        var pendingSpace = false;
        foreach (var c in value.Trim())
        {
            if (c is '\r' or '\n')
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                pendingSpace = false;
            }

            if (c == '\t' || c >= 128 || char.IsAsciiLetterOrDigit(c) || c == ' ')
            {
                if (!char.IsControl(c) || c == '\t')
                {
                    builder.Append(c);
                }
            }
            else if (!char.IsControl(c))
            {
                builder.Append('\\').Append(c);
            }
        }

        return builder.ToString();
    }
}
