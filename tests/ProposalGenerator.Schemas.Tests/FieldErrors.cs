using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace ProposalGenerator.Schemas.Tests;

/// <summary>A schema violation expressed as a template field path such as <c>customer.name</c>.</summary>
internal sealed record FieldError(string Path, string Keyword, string Message)
{
    public override string ToString() => $"{Path} [{Keyword}]: {Message}";
}

/// <summary>
/// Translates JsonSchema.Net list output into field paths. A <c>required</c> violation is reported by the
/// library at the parent object, so the missing property names are recovered from the schema's
/// <c>required</c> array and the instance, and appended to the path.
/// </summary>
internal static class FieldErrors
{
    public static IReadOnlyList<FieldError> From(EvaluationResults results, JsonElement instance)
    {
        var root = JsonNode.Parse(instance.GetRawText());
        var errors = new List<FieldError>();

        foreach (var node in Flatten(results))
        {
            if (node.Errors is null)
            {
                continue;
            }

            var pointer = node.InstanceLocation.ToString();
            foreach (var (keyword, message) in node.Errors)
            {
                if (keyword == "required")
                {
                    foreach (var missing in MissingRequired(node.SchemaLocation, root, pointer))
                    {
                        errors.Add(new FieldError(ToFieldPath(pointer, missing), keyword, message));
                    }
                }
                else
                {
                    // A false subschema (from additionalProperties: false) reports an empty keyword;
                    // name the keyword that produced it instead.
                    var name = keyword.Length > 0
                        ? keyword
                        : SchemaCatalog.JsonPointerSegments(node.EvaluationPath.ToString()).LastOrDefault() ?? keyword;
                    errors.Add(new FieldError(ToFieldPath(pointer, null), name, message));
                }
            }
        }

        return errors.DistinctBy(e => (e.Path, e.Keyword)).ToList();
    }

    /// <summary>Converts a JSON Pointer such as <c>/rfp/requirements/0/priority</c> into <c>rfp.requirements[0].priority</c>.</summary>
    public static string ToFieldPath(string pointer, string? child)
    {
        var builder = new StringBuilder();
        var segments = SchemaCatalog.JsonPointerSegments(pointer);
        if (child is not null)
        {
            segments = segments.Append(child);
        }

        foreach (var segment in segments)
        {
            if (int.TryParse(segment, out _) && builder.Length > 0)
            {
                builder.Append('[').Append(segment).Append(']');
            }
            else
            {
                if (builder.Length > 0)
                {
                    builder.Append('.');
                }

                builder.Append(segment);
            }
        }

        return builder.ToString();
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

    private static IEnumerable<string> MissingRequired(Uri schemaLocation, JsonNode? root, string pointer)
    {
        var (schemaNode, _) = SchemaCatalog.Instance.Resolve(schemaLocation);
        var required = schemaNode["required"]?.AsArray().Select(n => n!.GetValue<string>()).ToList()
            ?? throw new InvalidOperationException($"'{schemaLocation}' reported 'required' but has no required array.");

        var target = root;
        foreach (var segment in SchemaCatalog.JsonPointerSegments(pointer))
        {
            target = target is JsonArray array ? array[int.Parse(segment)] : target?[segment];
        }

        var present = target as JsonObject
            ?? throw new InvalidOperationException($"Instance location '{pointer}' is not an object.");
        return required.Where(name => !present.ContainsKey(name));
    }
}
