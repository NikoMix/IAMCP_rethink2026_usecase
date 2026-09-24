namespace ProposalGenerator.Rendering;

/// <summary>Base type for every failure the renderer reports. No document is produced when one is thrown.</summary>
public abstract class DocumentRenderingException : Exception
{
    /// <summary>Initializes the exception.</summary>
    protected DocumentRenderingException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Why a required field counts as missing.</summary>
public enum MissingFieldReason
{
    /// <summary>The property, or one of its parent objects, does not exist.</summary>
    Absent,

    /// <summary>The value is JSON <c>null</c>.</summary>
    Null,

    /// <summary>The value is an empty or whitespace-only string, or an empty array.</summary>
    Empty,

    /// <summary>A parent segment of the path is not a JSON object.</summary>
    ParentNotObject,
}

/// <summary>One required field that the data does not supply.</summary>
/// <param name="Path">The dotted field path from the template's <c>required_fields</c>, for example <c>customer.name</c>.</param>
/// <param name="Reason">Why the field counts as missing.</param>
public sealed record MissingField(string Path, MissingFieldReason Reason);

/// <summary>The data does not supply every field that the template declares in <c>required_fields</c>.</summary>
public sealed class RequiredFieldsMissingException : DocumentRenderingException
{
    /// <summary>Initializes the exception.</summary>
    public RequiredFieldsMissingException(string templateId, IReadOnlyList<MissingField> missingFields)
        : base(BuildMessage(templateId, missingFields))
    {
        TemplateId = templateId;
        MissingFields = missingFields;
    }

    /// <summary>The template whose requirements were not met.</summary>
    public string TemplateId { get; }

    /// <summary>Every missing field, in the order the template declares them.</summary>
    public IReadOnlyList<MissingField> MissingFields { get; }

    /// <summary>The paths of every missing field.</summary>
    public IReadOnlyList<string> MissingPaths => MissingFields.Select(f => f.Path).ToArray();

    private static string BuildMessage(string templateId, IReadOnlyList<MissingField> missingFields) =>
        $"Template '{templateId}' requires {missingFields.Count} field(s) that the data does not supply: " +
        string.Join(", ", missingFields.Select(f => $"{f.Path} ({f.Reason})")) + ".";
}

/// <summary>The template file cannot be loaded or its front matter is invalid.</summary>
public sealed class TemplateDefinitionException : DocumentRenderingException
{
    /// <summary>Initializes the exception.</summary>
    public TemplateDefinitionException(string source, string message, Exception? innerException = null)
        : base($"Template '{source}': {message}", innerException)
    {
        Source = source;
    }

    /// <summary>The template file or identifier that failed to load.</summary>
    public new string Source { get; }
}

/// <summary>A syntax problem in a template, with a 1-based position in the template file.</summary>
/// <param name="Line">1-based line in the template file, including the front matter.</param>
/// <param name="Column">1-based column.</param>
/// <param name="Message">What is wrong.</param>
public sealed record TemplateSyntaxError(int Line, int Column, string Message)
{
    /// <inheritdoc />
    public override string ToString() => $"({Line},{Column}): {Message}";
}

/// <summary>The template does not parse, or it uses constructs outside the supported template subset.</summary>
public sealed class TemplateSyntaxException : DocumentRenderingException
{
    /// <summary>Initializes the exception.</summary>
    public TemplateSyntaxException(string templateId, IReadOnlyList<TemplateSyntaxError> errors)
        : base($"Template '{templateId}' has {errors.Count} syntax error(s): " + string.Join("; ", errors))
    {
        TemplateId = templateId;
        Errors = errors;
    }

    /// <summary>The template that failed.</summary>
    public string TemplateId { get; }

    /// <summary>Every error found, ordered by position.</summary>
    public IReadOnlyList<TemplateSyntaxError> Errors { get; }
}

/// <summary>The template parsed, but evaluating it against the data failed, for example because it writes a whole object as text.</summary>
public sealed class TemplateEvaluationException : DocumentRenderingException
{
    /// <summary>Initializes the exception.</summary>
    public TemplateEvaluationException(string templateId, TemplateSyntaxError error, Exception? innerException = null)
        : base($"Template '{templateId}' failed to render at {error}", innerException)
    {
        TemplateId = templateId;
        Error = error;
    }

    /// <summary>The template that failed.</summary>
    public string TemplateId { get; }

    /// <summary>Where and why evaluation failed.</summary>
    public TemplateSyntaxError Error { get; }
}

/// <summary>The rendered Markdown contains a construct the DOCX writer does not support.</summary>
public sealed class UnsupportedMarkdownException : DocumentRenderingException
{
    /// <summary>Initializes the exception.</summary>
    public UnsupportedMarkdownException(string construct, int line)
        : base($"Markdown construct '{construct}' at rendered line {line} is not supported by the DOCX writer.")
    {
        Construct = construct;
        Line = line;
    }

    /// <summary>The Markdig node type that is not supported.</summary>
    public string Construct { get; }

    /// <summary>The 1-based line in the rendered Markdown.</summary>
    public int Line { get; }
}

/// <summary>The produced DOCX package failed Open XML SDK validation.</summary>
public sealed class DocxValidationException : DocumentRenderingException
{
    /// <summary>Initializes the exception.</summary>
    public DocxValidationException(IReadOnlyList<string> errors)
        : base($"The produced DOCX has {errors.Count} Open XML validation error(s): " + string.Join("; ", errors.Take(10)))
    {
        Errors = errors;
    }

    /// <summary>Every validation error, formatted as <c>part path: description</c>.</summary>
    public IReadOnlyList<string> Errors { get; }
}
