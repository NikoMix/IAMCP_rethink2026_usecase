using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProposalGenerator.Agent.Schemas;

/// <summary>JSON parsing that rejects duplicate property names up front.</summary>
/// <remarks>
/// <see cref="JsonNode.Parse(string, JsonNodeOptions?, JsonDocumentOptions)"/> accepts duplicates and only throws an
/// <see cref="ArgumentException"/> when an object is first accessed, which would escape the typed error paths.
/// </remarks>
internal static class StrictJson
{
    /// <exception cref="JsonException">The text is not valid JSON or contains a duplicate property.</exception>
    public static JsonNode? Parse(string text)
    {
        var node = JsonNode.Parse(text);
        EnsureUniqueProperties(node, "$");
        return node;
    }

    private static void EnsureUniqueProperties(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject obj:
                KeyValuePair<string, JsonNode?>[] properties;
                try
                {
                    properties = obj.ToArray();
                }
                catch (ArgumentException ex)
                {
                    throw new JsonException($"Duplicate property in the object at {path}: {ex.Message}", ex);
                }

                foreach (var (name, value) in properties)
                {
                    EnsureUniqueProperties(value, $"{path}.{name}");
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    EnsureUniqueProperties(array[i], $"{path}[{i}]");
                }

                break;
        }
    }
}
