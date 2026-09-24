using System.Text.Json;

namespace ProposalGenerator.Rendering;

/// <summary>Renders validated document data into Word documents.</summary>
public interface IDocumentRenderer
{
    /// <summary>
    /// Renders <paramref name="data"/> into a DOCX document using the template for <paramref name="documentType"/>.
    /// </summary>
    /// <param name="documentType">The document type whose template is used.</param>
    /// <param name="data">The document data. The root must be a JSON object.</param>
    /// <param name="options">Rendering options; use <see cref="RenderOptions.Default"/> for the defaults.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rendered document. No document is returned when rendering fails.</returns>
    /// <exception cref="RequiredFieldsMissingException">The data lacks one or more required fields.</exception>
    /// <exception cref="TemplateDefinitionException">The template file is missing or its front matter is invalid.</exception>
    /// <exception cref="TemplateSyntaxException">The template uses syntax outside the supported subset.</exception>
    /// <exception cref="UnsupportedMarkdownException">The rendered Markdown contains a construct the DOCX writer does not support.</exception>
    /// <exception cref="DocxValidationException">The produced package failed Open XML validation.</exception>
    Task<RenderedDocument> RenderDocxAsync(
        DocumentType documentType,
        JsonElement data,
        RenderOptions options,
        CancellationToken cancellationToken = default);
}
