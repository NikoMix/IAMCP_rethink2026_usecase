using System.Text.Json.Nodes;

namespace ProposalGenerator.Agent.Schemas;

/// <summary>
/// Builds the self-contained JSON schema sent to the model as the response format.
/// </summary>
/// <remarks>
/// Transformations, all mechanical and lossless for validation semantics:
/// <list type="bullet">
/// <item>Each document type schema and every schema file it references is embedded under <c>$defs/&lt;file key&gt;</c>.</item>
/// <item>Every <c>$ref</c> is rewritten to a local pointer, for example
/// <c>common.schema.json#/$defs/party</c> becomes <c>#/$defs/common/$defs/party</c>.</item>
/// <item><c>$id</c> and <c>$schema</c> are removed from embedded file roots so that local pointers resolve against the bundle root.</item>
/// <item>The envelope's <c>document</c> becomes <c>anyOf</c> the document type schemas or <c>null</c>, and
/// <c>documentType</c> is restricted to the configured document types.</item>
/// </list>
/// Replies are still validated against the original schema files, never against this bundle.
/// </remarks>
internal static class ModelSchemaBuilder
{
    public static JsonObject? TryBuild(
        JsonObject envelope,
        IReadOnlyList<SchemaFile> files,
        IReadOnlyList<string> documentTypes,
        List<string> errors)
    {
        var errorCount = errors.Count;
        var byBaseUri = files.ToDictionary(f => StripFragment(f.BaseUri), f => f);
        var byKey = files.ToDictionary(f => f.Key, f => f, StringComparer.Ordinal);

        var embedded = new JsonObject();
        var pending = new Queue<SchemaFile>(documentTypes.Select(t => byKey[t]));
        var queued = new HashSet<string>(documentTypes, StringComparer.Ordinal);

        while (pending.Count > 0)
        {
            var file = pending.Dequeue();
            var copy = (JsonObject)file.Source.DeepClone();
            copy.Remove("$id");
            copy.Remove("$schema");

            RewriteReferences(copy, file, byBaseUri, errors, target =>
            {
                if (queued.Add(target.Key))
                {
                    pending.Enqueue(target);
                }
            });

            embedded[file.Key] = copy;
        }

        var root = (JsonObject)envelope.DeepClone();
        root.Remove("$id");
        root.Remove("$schema");

        if (root["properties"] is not JsonObject properties)
        {
            errors.Add("The response envelope schema must declare 'properties'.");
            return null;
        }

        if (root.ContainsKey("$defs"))
        {
            errors.Add("The response envelope schema must not declare '$defs'; they are generated from the document schemas.");
            return null;
        }

        var typeEnum = new JsonArray();
        foreach (var type in documentTypes)
        {
            typeEnum.Add(type);
        }

        typeEnum.Add(null);
        properties["documentType"] = new JsonObject
        {
            ["description"] = "Selected document type, or null while it is unknown.",
            ["type"] = new JsonArray("string", "null"),
            ["enum"] = typeEnum,
        };

        var alternatives = new JsonArray();
        foreach (var type in documentTypes)
        {
            alternatives.Add(new JsonObject { ["$ref"] = $"#/$defs/{type}" });
        }

        alternatives.Add(new JsonObject { ["type"] = "null" });
        properties["document"] = new JsonObject
        {
            ["description"] = "Document data collected so far, or null. Complete and valid against its document type schema when the status is awaiting_confirmation or complete.",
            ["anyOf"] = alternatives,
        };

        root["$defs"] = embedded;
        return errors.Count > errorCount ? null : root;
    }

    private static void RewriteReferences(
        JsonNode? node,
        SchemaFile file,
        IReadOnlyDictionary<Uri, SchemaFile> byBaseUri,
        List<string> errors,
        Action<SchemaFile> onReference)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToArray())
                {
                    if (name == "$ref" && value is JsonValue refValue && refValue.TryGetValue(out string? reference))
                    {
                        var rewritten = Rewrite(reference, file, byBaseUri, errors, onReference);
                        if (rewritten is not null)
                        {
                            obj["$ref"] = rewritten;
                        }
                    }
                    else
                    {
                        RewriteReferences(value, file, byBaseUri, errors, onReference);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    RewriteReferences(item, file, byBaseUri, errors, onReference);
                }

                break;
        }
    }

    private static string? Rewrite(
        string reference,
        SchemaFile file,
        IReadOnlyDictionary<Uri, SchemaFile> byBaseUri,
        List<string> errors,
        Action<SchemaFile> onReference)
    {
        SchemaFile target;
        string pointer;

        if (reference.StartsWith('#'))
        {
            target = file;
            pointer = reference[1..];
        }
        else
        {
            if (!Uri.TryCreate(file.BaseUri, reference, out var absolute) ||
                !byBaseUri.TryGetValue(StripFragment(absolute), out target!))
            {
                errors.Add($"Unresolved $ref '{reference}' in '{file.Path}': no schema file in the schema directory has that URI.");
                return null;
            }

            pointer = Uri.UnescapeDataString(absolute.Fragment.TrimStart('#'));
        }

        if (pointer.Length > 0 && !pointer.StartsWith('/'))
        {
            errors.Add($"Unsupported $ref '{reference}' in '{file.Path}': only JSON Pointer fragments are supported.");
            return null;
        }

        if (!PointerExists(target.Source, pointer))
        {
            errors.Add($"Unresolved $ref '{reference}' in '{file.Path}': '{pointer}' does not exist in '{target.Path}'.");
            return null;
        }

        onReference(target);
        return $"#/$defs/{target.Key}{pointer}";
    }

    private static bool PointerExists(JsonNode root, string pointer)
    {
        JsonNode? current = root;
        foreach (var raw in pointer.Split('/').Skip(1))
        {
            var segment = raw.Replace("~1", "/").Replace("~0", "~");
            current = current switch
            {
                JsonObject obj when obj.TryGetPropertyValue(segment, out var child) => child,
                JsonArray array when int.TryParse(segment, out var index) && index >= 0 && index < array.Count => array[index],
                _ => null,
            };

            if (current is null)
            {
                return false;
            }
        }

        return true;
    }

    private static Uri StripFragment(Uri uri) => new(uri.GetLeftPart(UriPartial.Query));
}
