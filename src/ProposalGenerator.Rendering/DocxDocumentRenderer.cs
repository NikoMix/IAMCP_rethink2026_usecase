using System.Text.Json;
using ProposalGenerator.Rendering.Data;
using ProposalGenerator.Rendering.Docx;
using ProposalGenerator.Rendering.Templates;

namespace ProposalGenerator.Rendering;

/// <summary>
/// Renders documents in four steps: load the template and its front matter, check the required
/// fields, render the template text to Markdown with Scriban (Liquid mode), and convert the
/// Markdown to DOCX with Markdig and the Open XML SDK.
/// </summary>
public sealed class DocxDocumentRenderer : IDocumentRenderer
{
    private readonly ITemplateSource templateSource;
    private readonly IDocxStyleSheet styleSheet;
    private readonly IReadOnlyList<IDocxPostProcessor> postProcessors;

    /// <summary>Initializes the renderer.</summary>
    /// <param name="templateSource">Supplies the template files.</param>
    /// <param name="styleSheet">The document styles; defaults to <see cref="DefaultDocxStyleSheet"/>.</param>
    /// <param name="postProcessors">Package adjustments that run after the body is written, in order.</param>
    public DocxDocumentRenderer(
        ITemplateSource templateSource,
        IDocxStyleSheet? styleSheet = null,
        IEnumerable<IDocxPostProcessor>? postProcessors = null)
    {
        ArgumentNullException.ThrowIfNull(templateSource);
        this.templateSource = templateSource;
        this.styleSheet = styleSheet ?? DefaultDocxStyleSheet.Instance;
        this.postProcessors = postProcessors?.ToArray() ?? [];
    }

    /// <inheritdoc />
    public async Task<RenderedDocument> RenderDocxAsync(
        DocumentType documentType,
        JsonElement data,
        RenderOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (data.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"The document data must be a JSON object, not {data.ValueKind}.", nameof(data));
        }

        var source = await templateSource.GetTemplateAsync(documentType, cancellationToken).ConfigureAwait(false);
        if (source.DocumentType != documentType)
        {
            throw new TemplateDefinitionException(source.SourceName, $"The template source returned a {source.DocumentType} template for {documentType}.");
        }

        var definition = TemplateDefinitionParser.Parse(source);
        var markdown = RenderMarkdown(definition, data, cancellationToken);

        var context = new DocxRenderContext(documentType, definition.TemplateId, definition.Version, data, options);
        var content = DocxPackageBuilder.Build(markdown, styleSheet, postProcessors, context, cancellationToken);
        return new RenderedDocument(documentType, definition.TemplateId, definition.Version, content);
    }

    /// <summary>Runs the template steps only and returns the intermediate Markdown.</summary>
    internal static string RenderMarkdown(TemplateDefinition definition, JsonElement data, CancellationToken cancellationToken)
    {
        var missing = RequiredFieldValidator.FindMissing(data, definition.RequiredFields);
        if (missing.Count > 0)
        {
            throw new RequiredFieldsMissingException(definition.TemplateId, missing);
        }

        var compiled = ScribanTemplateRenderer.Compile(definition);
        cancellationToken.ThrowIfCancellationRequested();
        return ScribanTemplateRenderer.Render(compiled, data, cancellationToken);
    }
}
