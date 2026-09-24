using System.Diagnostics.CodeAnalysis;

namespace ProposalGenerator.Validation;

/// <summary>Commercial document types supported by the proposal generator.</summary>
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

/// <summary>Converts between <see cref="DocumentType"/> and its wire name, for example <c>change-request</c>.</summary>
public static class DocumentTypeNames
{
    private static readonly Dictionary<string, DocumentType> ByName = new(StringComparer.Ordinal)
    {
        ["sow"] = DocumentType.Sow,
        ["rfi"] = DocumentType.Rfi,
        ["rfp"] = DocumentType.Rfp,
        ["msa"] = DocumentType.Msa,
        ["change-request"] = DocumentType.ChangeRequest,
    };

    /// <summary>All wire names in declaration order.</summary>
    public static IReadOnlyList<string> All { get; } = ["sow", "rfi", "rfp", "msa", "change-request"];

    /// <summary>Parses a wire name. Matching is exact and case-sensitive, as in the JSON Schemas.</summary>
    public static bool TryParse(string? name, [NotNullWhen(true)] out DocumentType? type)
    {
        if (name is not null && ByName.TryGetValue(name, out var parsed))
        {
            type = parsed;
            return true;
        }

        type = null;
        return false;
    }

    /// <summary>Returns the wire name of a document type.</summary>
    public static string ToName(this DocumentType type) => type switch
    {
        DocumentType.Sow => "sow",
        DocumentType.Rfi => "rfi",
        DocumentType.Rfp => "rfp",
        DocumentType.Msa => "msa",
        DocumentType.ChangeRequest => "change-request",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown document type."),
    };
}
