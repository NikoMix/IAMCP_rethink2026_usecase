using System.Text.Json.Nodes;
using ProposalGenerator.Agent.StructuredOutput;

namespace ProposalGenerator.Agent.Tests;

/// <summary>
/// Parity with the Data model session's reference examples (Fixtures/schema-examples, snapshot of
/// schemas/examples): every valid example is accepted as a final reply, and every invalid example is
/// rejected at the path and keyword listed in expected-errors.json.
/// </summary>
public class SchemaExampleParityTests
{
    private static readonly string ExamplesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema-examples");

    private static JsonObject ExpectedErrors() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(ExamplesDirectory, "invalid", "expected-errors.json")))!.AsObject();

    private static string[] ValidFileNames() =>
        Directory.GetFiles(Path.Combine(ExamplesDirectory, "valid"), "*.example.json").Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal).ToArray();

    public static TheoryData<string> ValidExamples() => new(ValidFileNames());

    public static TheoryData<string> InvalidExamples() => new(ExpectedErrors().Select(e => e.Key).Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void Example_population_covers_every_document_type()
    {
        var definition = TestConfiguration.LoadRealDefinition();
        var invalidFiles = Directory.GetFiles(Path.Combine(ExamplesDirectory, "invalid"), "*.json")
            .Select(p => Path.GetFileName(p))
            .Where(n => n != "expected-errors.json")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(5, ValidFileNames().Length);
        Assert.Equal(12, invalidFiles.Length);
        Assert.Equal(invalidFiles, ExpectedErrors().Select(e => e.Key).Order(StringComparer.Ordinal));
        Assert.Equal(
            definition.ResponseFormat.Schemas.DocumentTypes.Order(StringComparer.Ordinal),
            ValidFileNames().Select(n => n.Replace(".example.json", string.Empty, StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(ValidExamples))]
    public void Valid_example_is_accepted_as_a_final_reply(string fileName)
    {
        var documentType = fileName.Replace(".example.json", string.Empty, StringComparison.Ordinal);
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(ExamplesDirectory, "valid", fileName)))!.AsObject();

        var result = new AgentReplyValidator(TestConfiguration.LoadRealDefinition().ResponseFormat.Schemas).Validate(
            Replies.Envelope("awaiting_confirmation", documentType, document), [Replies.Plausibility(new JsonObject())]);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [MemberData(nameof(InvalidExamples))]
    public void Invalid_example_is_rejected_at_the_expected_field(string fileName)
    {
        var expected = ExpectedErrors()[fileName]!;
        var documentType = expected["documentType"]!.GetValue<string>();
        var keyword = expected["keyword"]!.GetValue<string>();
        var segments = expected["path"]!.GetValue<string>().Replace("[", ".", StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal).Split('.');
        var pointer = "/document/" + string.Join('/', segments);
        var parent = "/document" + string.Concat(segments[..^1].Select(s => "/" + s));
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(ExamplesDirectory, "invalid", fileName)))!.AsObject();

        var result = new AgentReplyValidator(TestConfiguration.LoadRealDefinition().ResponseFormat.Schemas).Validate(
            Replies.Envelope("awaiting_confirmation", documentType, document), [Replies.Plausibility(new JsonObject())]);

        Assert.False(result.IsValid);
        // Object-level keywords such as required report the parent object and name the property.
        Assert.Contains(result.Errors, e => e.Keyword == keyword
            && (e.Path == pointer || (e.Path == parent && e.Message.Contains(segments[^1], StringComparison.Ordinal))));
    }
}
