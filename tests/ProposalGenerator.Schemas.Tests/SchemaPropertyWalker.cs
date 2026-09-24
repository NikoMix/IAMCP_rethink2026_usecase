using System.Text.Json.Nodes;

namespace ProposalGenerator.Schemas.Tests;

/// <summary>
/// Walks a dotted field path such as <c>supplier.signatory.name</c> through a document schema, following
/// <c>$ref</c> and <c>allOf</c>, and reports whether each segment is declared and required.
/// </summary>
internal static class SchemaPropertyWalker
{
    /// <summary>Returns a description of the first problem found, or <see langword="null"/> when the path is valid.</summary>
    public static string? Check(string documentType, string fieldPath, bool mustBeRequired)
    {
        var catalog = SchemaCatalog.Instance;
        var current = new List<(JsonNode Node, Uri BaseId)> { (catalog.RawSchema(documentType), catalog.IdOfType(documentType)) };
        var walked = new List<string>();

        foreach (var segment in fieldPath.Split('.'))
        {
            var facets = current.SelectMany(Expand).ToList();

            if (facets.Any(f => f.Node["type"]?.GetValue<string>() == "array"))
            {
                facets = facets
                    .Where(f => f.Node["items"] is not null)
                    .SelectMany(f => Expand((f.Node["items"]!, f.BaseId)))
                    .ToList();
            }

            var declared = facets
                .Where(f => f.Node["properties"]?[segment] is not null)
                .Select(f => (Node: f.Node["properties"]![segment]!, f.BaseId))
                .ToList();

            var here = walked.Count == 0 ? "the document root" : string.Join('.', walked);
            if (declared.Count == 0)
            {
                return $"'{segment}' is not a property of {here}.";
            }

            if (mustBeRequired && !facets.Any(f => Required(f.Node).Contains(segment)))
            {
                return $"'{segment}' is declared but not required at {here}.";
            }

            walked.Add(segment);
            current = declared;
        }

        return null;
    }

    private static IEnumerable<(JsonNode Node, Uri BaseId)> Expand((JsonNode Node, Uri BaseId) schema)
    {
        yield return schema;

        if (schema.Node["$ref"]?.GetValue<string>() is { } reference)
        {
            var target = SchemaCatalog.Instance.Resolve(new Uri(schema.BaseId, reference));
            foreach (var nested in Expand(target))
            {
                yield return nested;
            }
        }

        foreach (var branch in schema.Node["allOf"]?.AsArray() ?? [])
        {
            foreach (var nested in Expand((branch!, schema.BaseId)))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<string> Required(JsonNode node) =>
        node["required"]?.AsArray().Select(n => n!.GetValue<string>()) ?? [];
}
