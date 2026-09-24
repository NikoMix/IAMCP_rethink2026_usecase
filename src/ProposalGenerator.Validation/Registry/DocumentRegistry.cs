using System.Text.Json;

namespace ProposalGenerator.Validation.Registry;

/// <summary>A stored document version that other documents can reference, for example the SOW a change request amends.</summary>
/// <param name="Type">Document type.</param>
/// <param name="Number">Business number, for example <c>SOW-2026-014</c>.</param>
/// <param name="Version">Version, for example <c>1.0</c>.</param>
/// <param name="Content">Document JSON in the shape of its schema.</param>
public sealed record RegisteredDocument(DocumentType Type, string Number, string Version, JsonElement Content);

/// <summary>
/// Looks up predecessor documents. The host (for example the agent service) implements it over its document store;
/// the plausibility check never guesses a predecessor.
/// </summary>
public interface IDocumentRegistry
{
    /// <summary>Returns all stored versions of a document, or an empty list when the number is unknown.</summary>
    ValueTask<IReadOnlyList<RegisteredDocument>> FindAsync(DocumentType type, string number, CancellationToken cancellationToken = default);
}

/// <summary>In-memory registry for tests, demos and hosts that load documents at startup.</summary>
public sealed class InMemoryDocumentRegistry : IDocumentRegistry
{
    private readonly List<RegisteredDocument> _documents = [];

    /// <summary>Adds a document version. The JSON content is cloned, so the caller may dispose its document.</summary>
    public InMemoryDocumentRegistry Add(DocumentType type, string number, string version, JsonElement content)
    {
        _documents.Add(new RegisteredDocument(type, number, version, content.Clone()));
        return this;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<RegisteredDocument>> FindAsync(DocumentType type, string number, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<RegisteredDocument>>(
            _documents.Where(d => d.Type == type && string.Equals(d.Number, number, StringComparison.Ordinal)).ToArray());
}
