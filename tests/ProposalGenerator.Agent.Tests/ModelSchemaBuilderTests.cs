using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace ProposalGenerator.Agent.Tests;

public class ModelSchemaBuilderTests
{
    private static JsonObject ModelSchema() => TestConfiguration.LoadRealDefinition().ResponseFormat.ModelSchema;

    [Fact]
    public void Bundle_contains_only_local_references()
    {
        var references = References(ModelSchema()).ToList();

        Assert.NotEmpty(references);
        Assert.All(references, r => Assert.StartsWith("#/$defs/", r));
    }

    [Fact]
    public void Bundle_embeds_every_document_type_and_the_shared_schema_without_ids()
    {
        var defs = ModelSchema()["$defs"]!.AsObject();

        Assert.Equal(["change-request", "common", "msa", "rfi", "rfp", "sow"], defs.Select(d => d.Key).Order(StringComparer.Ordinal));
        Assert.All(defs, d =>
        {
            Assert.False(d.Value!.AsObject().ContainsKey("$id"));
            Assert.False(d.Value!.AsObject().ContainsKey("$schema"));
        });
    }

    [Fact]
    public void Bundle_restricts_document_type_and_document()
    {
        var properties = ModelSchema()["properties"]!.AsObject();

        Assert.Equal(["rfi", "rfp", "msa", "sow", "change-request", null],
            properties["documentType"]!["enum"]!.AsArray().Select(n => n?.GetValue<string>()));
        Assert.Equal(6, properties["document"]!["anyOf"]!.AsArray().Count);
    }

    [Fact]
    public void Bundle_is_self_contained_and_accepts_a_valid_document()
    {
        // A fresh registry proves the bundle needs no external schema file.
        var schema = JsonSchema.FromText(ModelSchema().ToJsonString(), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        var reply = Replies.Envelope("awaiting_confirmation", "sow", TestPaths.ValidSow());

        var valid = schema.Evaluate(JsonDocument.Parse(reply).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(valid.IsValid);
    }

    [Fact]
    public void Bundle_rejects_a_document_the_original_schema_rejects()
    {
        var schema = JsonSchema.FromText(ModelSchema().ToJsonString(), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        var sow = TestPaths.ValidSow();
        sow["pricing"]!.AsObject().Remove("currency");
        var reply = Replies.Envelope("awaiting_confirmation", "sow", sow);

        var result = schema.Evaluate(JsonDocument.Parse(reply).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Original_schemas_are_not_modified_by_bundling()
    {
        var definition = TestConfiguration.LoadRealDefinition();
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(TestPaths.FixtureSchemas, "sow.schema.json")))!;

        var files = definition.ResponseFormat.Schemas.Files;

        Assert.True(JsonNode.DeepEquals(original, files.Single(f => f.Key == "sow").Source));
    }

    private static IEnumerable<string> References(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    if (name == "$ref")
                    {
                        yield return value!.GetValue<string>();
                    }
                    else
                    {
                        foreach (var nested in References(value))
                        {
                            yield return nested;
                        }
                    }
                }

                break;
            case JsonArray array:
                foreach (var nested in array.SelectMany(References))
                {
                    yield return nested;
                }

                break;
        }
    }
}
