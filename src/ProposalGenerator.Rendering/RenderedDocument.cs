namespace ProposalGenerator.Rendering;

/// <summary>A rendered DOCX document.</summary>
public sealed class RenderedDocument
{
    /// <summary>The MIME type of a Word document.</summary>
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly byte[] content;

    internal RenderedDocument(DocumentType documentType, string templateId, string templateVersion, byte[] content)
    {
        DocumentType = documentType;
        TemplateId = templateId;
        TemplateVersion = templateVersion;
        this.content = content;
    }

    /// <summary>The document type that was rendered.</summary>
    public DocumentType DocumentType { get; }

    /// <summary>The <c>template_id</c> from the template front matter.</summary>
    public string TemplateId { get; }

    /// <summary>The <c>version</c> from the template front matter.</summary>
    public string TemplateVersion { get; }

    /// <summary>The MIME type of <see cref="Content"/>.</summary>
    public string ContentType => DocxContentType;

    /// <summary>The DOCX package bytes. The array is a copy; changing it does not affect this instance.</summary>
    public byte[] Content => (byte[])content.Clone();

    /// <summary>The package size in bytes.</summary>
    public int Length => content.Length;

    /// <summary>Opens a read-only stream over the DOCX package.</summary>
    public Stream OpenRead() => new MemoryStream(content, writable: false);
}
