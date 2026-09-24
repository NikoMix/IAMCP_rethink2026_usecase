using System.Text.Json;
using System.Text.Json.Nodes;
using static ProposalGenerator.Validation.FindingCodes;

namespace ProposalGenerator.Validation.Tests;

public sealed class PlausibilityToolTests
{
    private static readonly PlausibilityTool Tool = new(Fixture.FullOptions());

    private static JsonElement Arguments(string documentType, JsonNode? document)
    {
        var arguments = new JsonObject { ["documentType"] = documentType, ["document"] = document };
        return Fixture.ToElement(arguments);
    }

    [Fact]
    public void Definition_exposes_name_and_parameter_schema()
    {
        Assert.Equal("check_plausibility", Tool.Name);
        Assert.Equal(PlausibilityTool.ToolName, Tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(Tool.Description));

        var schema = Tool.ParametersSchema;
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal(["documentType", "document"], schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(DocumentTypeNames.All, schema.GetProperty("properties").GetProperty("documentType").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("object", schema.GetProperty("properties").GetProperty("document").GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public async Task Valid_arguments_round_trip_to_the_contract_shape()
    {
        var result = await Tool.HandleAsync(Arguments("change-request", Fixture.Load("change-request")));
        var json = JsonDocument.Parse(PlausibilityTool.SerializeResult(result)).RootElement;

        Assert.Equal(["findings", "computed"], json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(0, json.GetProperty("findings").GetArrayLength());
        Assert.Equal(196000m, json.GetProperty("computed").GetProperty("$.cr.impact.revised_value").GetDecimal());
    }

    [Fact]
    public async Task Findings_serialize_with_code_severity_path_and_german_message()
    {
        var document = Fixture.Load("sow");
        document.Obj("pricing").Item("payment_schedule", 1)["amount"] = 128000;
        document.Obj("project")["end_date"] = "2026-09-01";

        var result = await Tool.HandleAsync(Arguments("sow", document));
        var serialized = PlausibilityTool.SerializeResult(result);
        var findings = JsonDocument.Parse(serialized).RootElement.GetProperty("findings").EnumerateArray().ToArray();

        Assert.Equal(3, findings.Length);
        Assert.All(findings, f => Assert.Equal(["code", "severity", "path", "message"], f.EnumerateObject().Select(p => p.Name)));
        Assert.Equal(["error", "warning", "warning"], findings.Select(f => f.GetProperty("severity").GetString()));
        Assert.Equal(PaymentScheduleSumMismatch, findings[0].GetProperty("code").GetString());
        Assert.Equal("$.pricing.payment_schedule", findings[0].GetProperty("path").GetString());
        Assert.Contains("Die Summe des Zahlungsplans", findings[0].GetProperty("message").GetString());
        Assert.Contains("„Go-live“", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Findings_are_ordered_error_warning_info()
    {
        var document = Fixture.Load("sow");
        document.Obj("pricing")["total"] = 180000;
        document.Obj("sow").Item("milestones", 1)["date"] = "2026-10-15";

        var result = await Tool.HandleAsync(Arguments("sow", document));

        Assert.Equal(
            [FindingSeverity.Error, FindingSeverity.Warning, FindingSeverity.Info],
            result.Findings.Select(f => f.Severity).Distinct());
        Assert.Equal(result.Findings.OrderBy(f => f.Severity).Select(f => f.Code), result.Findings.Select(f => f.Code));
    }

    [Fact]
    public async Task Arguments_as_a_json_string_are_accepted()
    {
        var arguments = JsonSerializer.SerializeToElement(Arguments("rfi", Fixture.Load("rfi")).GetRawText());

        var result = await Tool.HandleAsync(arguments);

        FindingAssert.Clean(result);
    }

    [Fact]
    public async Task Result_is_deterministic()
    {
        var arguments = Arguments("sow", Fixture.Load("sow"));

        var first = PlausibilityTool.SerializeResult(await Tool.HandleAsync(arguments));
        var second = PlausibilityTool.SerializeResult(await new PlausibilityTool(Fixture.FullOptions()).HandleAsync(arguments));

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("[]", "arguments")]
    [InlineData("\"{ not json\"", "arguments")]
    [InlineData("{ \"documentType\": \"sow\" }", "document")]
    [InlineData("{ \"documentType\": \"sow\", \"document\": \"SOW-2026-001\" }", "document")]
    public async Task Malformed_arguments_are_reported_not_thrown(string arguments, string path)
    {
        var result = await Tool.HandleAsync(JsonDocument.Parse(arguments).RootElement);

        FindingAssert.Single(result, ArgumentsInvalid, FindingSeverity.Error, path);
        Assert.Single(result.Findings);
    }

    [Theory]
    [InlineData("{ \"document\": {} }")]
    [InlineData("{ \"documentType\": \"SOW\", \"document\": {} }")]
    [InlineData("{ \"documentType\": \"invoice\", \"document\": {} }")]
    [InlineData("{ \"documentType\": 1, \"document\": {} }")]
    public async Task Unknown_document_type_is_reported(string arguments)
    {
        var result = await Tool.HandleAsync(JsonDocument.Parse(arguments).RootElement);

        var finding = FindingAssert.Single(result, DocumentTypeInvalid, FindingSeverity.Error, "documentType");
        Assert.Contains("change-request", finding.Message);
    }

    [Fact]
    public async Task Checker_rejects_a_document_that_is_not_an_object()
    {
        var result = await new PlausibilityChecker().CheckAsync(DocumentType.Sow, JsonDocument.Parse("[1]").RootElement);

        FindingAssert.Single(result, ArgumentsInvalid, FindingSeverity.Error, "document");
    }
}

public sealed class FindingCatalogueTests
{
    private static readonly string[] ToolCodes = [ArgumentsInvalid, DocumentTypeInvalid];

    [Fact]
    public void Every_code_is_unique_and_produced_by_exactly_one_rule_or_the_tool()
    {
        var produced = PlausibilityChecker.Rules.SelectMany(r => r.Codes).Concat(ToolCodes).ToArray();

        Assert.Equal(FindingCodes.All.Distinct().Count(), FindingCodes.All.Count);
        Assert.Equal(produced.Distinct().Count(), produced.Length);
        Assert.Equal(FindingCodes.All.Order(), produced.Order());
    }

    [Fact]
    public void Catalogue_lists_every_public_code_constant()
    {
        var constants = typeof(FindingCodes).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToArray();

        Assert.Equal(35, constants.Length);
        Assert.Equal(constants.Order(), FindingCodes.All.Order());
        Assert.All(constants, c => Assert.Matches("^[A-Z][A-Z0-9_]+$", c));
    }
}

public sealed class FieldPathTests
{
    [Fact]
    public async Task Overridden_paths_are_used_for_reading_and_reporting()
    {
        var paths = PlausibilityFieldPaths.FromJson("""{ "PaymentSchedule": "pricing.payments", "PaymentAmount": "value" }""");
        var document = Fixture.Load("sow");
        var schedule = document.Obj("pricing")["payment_schedule"]!.AsArray();
        document.Obj("pricing").Remove("payment_schedule");
        foreach (var entry in schedule)
        {
            entry!["value"] = entry["amount"]!.GetValue<int>();
            entry.AsObject().Remove("amount");
        }

        schedule[1]!["value"] = 1;
        document.Obj("pricing")["payments"] = schedule;

        var result = await Fixture.CheckAsync(DocumentType.Sow, document, new PlausibilityCheckerOptions { FieldPaths = paths });

        FindingAssert.Single(result, PaymentScheduleSumMismatch, FindingSeverity.Error, "$.pricing.payments");
    }

    [Fact]
    public async Task Default_paths_do_not_see_renamed_fields()
    {
        var document = Fixture.Load("sow");
        var schedule = document.Obj("pricing")["payment_schedule"]!;
        document.Obj("pricing").Remove("payment_schedule");
        schedule[1]!["amount"] = 1;
        document.Obj("pricing")["payments"] = schedule;

        var result = await Fixture.CheckAsync(DocumentType.Sow, document);

        FindingAssert.None(result, PaymentScheduleSumMismatch);
    }

    [Fact]
    public void Unknown_path_name_is_rejected() =>
        Assert.Throws<JsonException>(() => PlausibilityFieldPaths.FromJson("""{ "PricingTotl": "pricing.total" }"""));

    [Theory]
    [InlineData("")]
    [InlineData("pricing..total")]
    [InlineData(" ")]
    public void Malformed_path_is_rejected(string path)
    {
        Assert.Throws<ArgumentException>(() => PlausibilityFieldPaths.FromJson(JsonSerializer.Serialize(new { PricingTotal = path })));
        Assert.Throws<ArgumentException>(() => new PlausibilityChecker(new PlausibilityCheckerOptions { FieldPaths = new PlausibilityFieldPaths { PricingTotal = path } }));
    }

    [Fact]
    public void Default_paths_are_valid() => new PlausibilityFieldPaths().Validate();
}
