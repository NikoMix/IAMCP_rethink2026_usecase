using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProposalGenerator.Rendering.Tests.Support;

/// <summary>
/// Fictional sample documents, one per document type, in <c>Fixtures/&lt;template-id&gt;.json</c>.
/// </summary>
/// <remarks>
/// The fixtures are copies of the valid examples the data model work (#7) publishes under
/// <c>schemas/examples/valid/</c>, without the <c>$schema</c> pointer. Replace them with
/// references to those files once <c>schemas/examples/</c> is on the base branch.
/// </remarks>
internal static class SampleData
{
    public static JsonElement Load(DocumentType documentType) => Parse(LoadText(documentType));

    public static JsonObject LoadNode(DocumentType documentType) =>
        JsonNode.Parse(LoadText(documentType))?.AsObject()
        ?? throw new InvalidOperationException($"Fixture for {documentType} is not a JSON object.");

    public static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public static JsonElement ToElement(JsonNode node) => Parse(node.ToJsonString());

    private static string LoadText(DocumentType documentType) =>
        File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory, documentType.ToTemplateId() + ".json"));
}
