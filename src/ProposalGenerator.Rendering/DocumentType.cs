namespace ProposalGenerator.Rendering;

/// <summary>
/// The commercial document types the generator can render. Each value maps to one
/// template file <c>templates/&lt;template-id&gt;.md</c>.
/// </summary>
public enum DocumentType
{
    /// <summary>Statement of Work (<c>sow</c>).</summary>
    Sow,

    /// <summary>Request for Information (<c>rfi</c>).</summary>
    Rfi,

    /// <summary>Request for Proposal (<c>rfp</c>).</summary>
    Rfp,

    /// <summary>Master Service Agreement (<c>msa</c>).</summary>
    Msa,

    /// <summary>Change Request (<c>change-request</c>).</summary>
    ChangeRequest,
}

/// <summary>Maps <see cref="DocumentType"/> values to and from their template identifiers.</summary>
public static class DocumentTypeExtensions
{
    private static readonly IReadOnlyDictionary<DocumentType, string> TemplateIds = new Dictionary<DocumentType, string>
    {
        [DocumentType.Sow] = "sow",
        [DocumentType.Rfi] = "rfi",
        [DocumentType.Rfp] = "rfp",
        [DocumentType.Msa] = "msa",
        [DocumentType.ChangeRequest] = "change-request",
    };

    /// <summary>All supported document types, in declaration order.</summary>
    public static IReadOnlyList<DocumentType> All { get; } = Array.AsReadOnly(Enum.GetValues<DocumentType>());

    /// <summary>
    /// Returns the template identifier, which is also the template file name without extension
    /// and the schema name (<c>schemas/&lt;id&gt;.schema.json</c>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined document type.</exception>
    public static string ToTemplateId(this DocumentType documentType) =>
        TemplateIds.TryGetValue(documentType, out var id)
            ? id
            : throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown document type.");

    /// <summary>Parses a template identifier such as <c>change-request</c>. Matching is case-sensitive.</summary>
    public static bool TryParseTemplateId(string? templateId, out DocumentType documentType)
    {
        foreach (var (type, id) in TemplateIds)
        {
            if (string.Equals(id, templateId, StringComparison.Ordinal))
            {
                documentType = type;
                return true;
            }
        }

        documentType = default;
        return false;
    }
}
