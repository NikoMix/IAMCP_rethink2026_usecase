using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace ProposalGenerator.Agent.Schemas;

/// <summary>
/// The response envelope schema plus the original document type schemas
/// (<c>schemas/&lt;doctype&gt;.schema.json</c>) used to validate every agent reply.
/// </summary>
public sealed class DocumentSchemaSet
{
    private const string SchemaFileSuffix = ".schema.json";

    private static readonly EvaluationOptions Evaluation = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    private readonly JsonSchema _envelope;
    private readonly IReadOnlyDictionary<string, JsonSchema> _documents;

    private DocumentSchemaSet(
        JsonSchema envelope,
        JsonObject envelopeSource,
        IReadOnlyDictionary<string, JsonSchema> documents,
        IReadOnlyList<string> documentTypes,
        IReadOnlyList<SchemaFile> files)
    {
        _envelope = envelope;
        _documents = documents;
        EnvelopeSource = envelopeSource;
        DocumentTypes = documentTypes;
        Files = files;
    }

    public IReadOnlyList<string> DocumentTypes { get; }

    internal JsonObject EnvelopeSource { get; }

    internal IReadOnlyList<SchemaFile> Files { get; }

    public bool Supports(string documentType) => _documents.ContainsKey(documentType);

    public IReadOnlyList<SchemaValidationError> ValidateEnvelope(JsonElement reply) =>
        Evaluate(_envelope, reply, string.Empty);

    /// <summary>Validates a document against the original schema of its type.</summary>
    /// <param name="pathPrefix">JSON Pointer of the document inside the reply, for example <c>/document</c>.</param>
    public IReadOnlyList<SchemaValidationError> ValidateDocument(string documentType, JsonElement document, string pathPrefix)
    {
        if (!_documents.TryGetValue(documentType, out var schema))
        {
            throw new ArgumentException($"Document type '{documentType}' is not configured.", nameof(documentType));
        }

        return Evaluate(schema, document, pathPrefix);
    }

    /// <summary>Loads and compiles the schemas; returns null and adds to <paramref name="errors"/> on failure.</summary>
    internal static DocumentSchemaSet? TryLoad(
        string envelopePath,
        string schemaDirectory,
        IReadOnlyList<string> documentTypes,
        List<string> errors)
    {
        var errorCount = errors.Count;
        var envelopeSource = ReadObject(envelopePath, errors);

        if (!Directory.Exists(schemaDirectory))
        {
            errors.Add($"responseFormat.schemaDirectory '{schemaDirectory}' does not exist.");
            return null;
        }

        var files = new List<SchemaFile>();
        foreach (var path in Directory.GetFiles(schemaDirectory, "*" + SchemaFileSuffix).Order(StringComparer.Ordinal))
        {
            var source = ReadObject(path, errors);
            if (source is null)
            {
                continue;
            }

            var fileName = Path.GetFileName(path);
            var key = fileName[..^SchemaFileSuffix.Length];
            files.Add(new SchemaFile(key, ResolveBaseUri(source, path), source, path));
        }

        foreach (var type in documentTypes)
        {
            if (!files.Any(f => f.Key == type))
            {
                errors.Add($"No schema file '{type}{SchemaFileSuffix}' for document type '{type}' in '{schemaDirectory}'.");
            }
        }

        if (errors.Count > errorCount || envelopeSource is null)
        {
            return null;
        }

        var buildOptions = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        var compiled = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);

        // Shared files first so that document schemas can reference them.
        foreach (var file in files.OrderBy(f => documentTypes.Contains(f.Key) ? 1 : 0))
        {
            var schema = Compile(file.Source, file.BaseUri, file.Path, buildOptions, errors);
            if (schema is not null)
            {
                compiled[file.Key] = schema;
            }
        }

        var envelope = Compile(envelopeSource, ResolveBaseUri(envelopeSource, envelopePath), envelopePath, buildOptions, errors);

        if (errors.Count > errorCount || envelope is null)
        {
            return null;
        }

        var documents = documentTypes.ToDictionary(t => t, t => compiled[t], StringComparer.Ordinal);
        return new DocumentSchemaSet(envelope, envelopeSource, documents, documentTypes.ToArray(), files);
    }

    private static IReadOnlyList<SchemaValidationError> Evaluate(JsonSchema schema, JsonElement instance, string pathPrefix)
    {
        var results = schema.Evaluate(instance, Evaluation);
        if (results.IsValid)
        {
            return [];
        }

        var errors = new List<SchemaValidationError>();
        foreach (var node in Flatten(results))
        {
            if (node.Errors is null)
            {
                continue;
            }

            foreach (var (keyword, message) in node.Errors)
            {
                var error = new SchemaValidationError(pathPrefix + node.InstanceLocation, keyword, message);
                if (!errors.Contains(error))
                {
                    errors.Add(error);
                }
            }
        }

        if (errors.Count == 0)
        {
            errors.Add(new SchemaValidationError(pathPrefix, "schema", "The value does not match the schema."));
        }

        return errors;
    }

    private static IEnumerable<EvaluationResults> Flatten(EvaluationResults results)
    {
        yield return results;
        foreach (var detail in results.Details ?? [])
        {
            foreach (var nested in Flatten(detail))
            {
                yield return nested;
            }
        }
    }

    private static JsonSchema? Compile(JsonObject source, Uri baseUri, string path, BuildOptions options, List<string> errors)
    {
        try
        {
            return JsonSchema.FromText(source.ToJsonString(), options, baseUri);
        }
        catch (Exception ex) when (ex is JsonSchemaException or JsonException or ArgumentException or InvalidOperationException)
        {
            errors.Add($"Schema '{path}' is not a valid JSON Schema: {ex.Message}");
            return null;
        }
    }

    private static JsonObject? ReadObject(string path, List<string> errors)
    {
        if (!File.Exists(path))
        {
            errors.Add($"Schema file '{path}' does not exist.");
            return null;
        }

        try
        {
            if (StrictJson.Parse(File.ReadAllText(path)) is JsonObject obj)
            {
                return obj;
            }

            errors.Add($"Schema file '{path}' must contain a JSON object.");
        }
        catch (JsonException ex)
        {
            errors.Add($"Schema file '{path}' is not valid JSON: {ex.Message}");
        }

        return null;
    }

    private static Uri ResolveBaseUri(JsonObject source, string path) =>
        source["$id"] is JsonValue id && id.TryGetValue(out string? value) && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : new Uri(Path.GetFullPath(path));
}

internal sealed record SchemaFile(string Key, Uri BaseUri, JsonObject Source, string Path);
