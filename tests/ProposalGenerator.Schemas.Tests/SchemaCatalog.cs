using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace ProposalGenerator.Schemas.Tests;

/// <summary>
/// Loads every schema under <c>schemas/</c> into one registry, so that the relative
/// <c>common.schema.json</c> references resolve offline, and keeps the raw JSON for structural checks.
/// </summary>
internal sealed class SchemaCatalog
{
    public const string CommonFileName = "common.schema.json";

    private readonly Dictionary<Uri, JsonNode> rawById = [];
    private readonly Dictionary<string, JsonSchema> schemasByType = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Uri> idsByFileName = new(StringComparer.Ordinal);

    public static SchemaCatalog Instance { get; } = new();

    private SchemaCatalog()
    {
        BuildOptions = new BuildOptions { SchemaRegistry = new SchemaRegistry() };

        var files = Directory.GetFiles(RepositoryLayout.SchemasDirectory, "*.schema.json")
            .OrderBy(path => Path.GetFileName(path) == CommonFileName ? 0 : 1)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var text = File.ReadAllText(file);
            var raw = JsonNode.Parse(text)
                ?? throw new InvalidDataException($"{fileName} is empty.");
            var id = new Uri(raw["$id"]?.GetValue<string>()
                ?? throw new InvalidDataException($"{fileName} has no $id."));

            rawById[id] = raw;
            idsByFileName[fileName] = id;

            var schema = JsonSchema.FromText(text, BuildOptions);
            if (fileName != CommonFileName)
            {
                schemasByType[fileName[..^".schema.json".Length]] = schema;
            }
        }
    }

    public BuildOptions BuildOptions { get; }

    public IReadOnlyCollection<string> FileNames => idsByFileName.Keys;

    public IReadOnlyCollection<string> DocumentTypes => schemasByType.Keys;

    public static EvaluationOptions EvaluationOptions => new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
        IncludeApplicatorErrors = false,
    };

    public Uri IdOf(string fileName) => idsByFileName[fileName];

    public JsonNode RawSchema(string documentType) => rawById[IdOf(documentType + ".schema.json")];

    public Uri IdOfType(string documentType) => IdOf(documentType + ".schema.json");

    public EvaluationResults Evaluate(string documentType, JsonElement instance) =>
        schemasByType[documentType].Evaluate(instance, EvaluationOptions);

    /// <summary>Builds a schema that only references <paramref name="reference"/>, relative to common.schema.json.</summary>
    public JsonSchema BuildReference(string reference)
    {
        var text = $$"""{ "$schema": "https://json-schema.org/draft/2020-12/schema", "$ref": "{{new Uri(IdOf(CommonFileName), reference)}}" }""";
        return JsonSchema.FromText(text, BuildOptions);
    }

    /// <summary>Resolves an absolute schema URI with an optional JSON Pointer fragment to its raw JSON node.</summary>
    public (JsonNode Node, Uri BaseId) Resolve(Uri location)
    {
        var documentId = new Uri(location.GetLeftPart(UriPartial.Query));
        if (!rawById.TryGetValue(documentId, out var node))
        {
            throw new KeyNotFoundException($"No schema is registered with $id '{documentId}'.");
        }

        var fragment = Uri.UnescapeDataString(location.Fragment.TrimStart('#'));
        foreach (var segment in JsonPointerSegments(fragment))
        {
            node = node is JsonArray array && int.TryParse(segment, out var index)
                ? array[index]
                : node[segment];
            if (node is null)
            {
                throw new KeyNotFoundException($"'{location}' does not resolve to a schema node.");
            }
        }

        return (node, documentId);
    }

    public static IEnumerable<string> JsonPointerSegments(string pointer) =>
        pointer.Length == 0
            ? []
            : pointer.TrimStart('/').Split('/').Select(s => s.Replace("~1", "/").Replace("~0", "~"));
}
