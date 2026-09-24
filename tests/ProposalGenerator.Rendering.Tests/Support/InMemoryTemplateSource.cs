using ProposalGenerator.Rendering.Templates;

namespace ProposalGenerator.Rendering.Tests.Support;

/// <summary>Serves test-owned template text, so engine tests do not depend on the repository templates.</summary>
internal sealed class InMemoryTemplateSource : ITemplateSource
{
    private readonly Dictionary<DocumentType, string> templates = [];

    public InMemoryTemplateSource Add(DocumentType documentType, string content)
    {
        templates[documentType] = content;
        return this;
    }

    public Task<TemplateSource> GetTemplateAsync(DocumentType documentType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return templates.TryGetValue(documentType, out var content)
            ? Task.FromResult(new TemplateSource(documentType, $"memory:{documentType.ToTemplateId()}.md", content))
            : throw new TemplateDefinitionException($"memory:{documentType.ToTemplateId()}.md", "The template file does not exist.");
    }

    /// <summary>Builds a template file with front matter for <paramref name="documentType"/>.</summary>
    public static string Template(DocumentType documentType, string body, params string[] requiredFields)
    {
        var required = requiredFields.Length == 0
            ? "required_fields: []"
            : "required_fields:\n" + string.Concat(requiredFields.Select(f => $"  - {f}\n")).TrimEnd('\n');
        return $"---\ntemplate_id: {documentType.ToTemplateId()}\ntitle: Test\nversion: 9.9.9\n{required}\n---\n{body}";
    }

    /// <summary>A renderer over a single SOW template with <paramref name="body"/>.</summary>
    public static DocxDocumentRenderer Renderer(string body, params string[] requiredFields) =>
        new(new InMemoryTemplateSource().Add(DocumentType.Sow, Template(DocumentType.Sow, body, requiredFields)));
}
