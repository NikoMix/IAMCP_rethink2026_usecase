using System.Text.Json;

namespace ProposalGenerator.Schemas.Tests;

/// <summary>Behaviour of the shared definitions in common.schema.json.</summary>
public sealed class CommonDefinitionTests
{
    [Theory]
    [InlineData("#/$defs/money", """{ "amount": 12000.50, "currency": "EUR" }""", true)]
    [InlineData("#/$defs/money", """{ "amount": "12000", "currency": "EUR" }""", false)]
    [InlineData("#/$defs/money", """{ "amount": 12000, "currency": "eur" }""", false)]
    [InlineData("#/$defs/money", """{ "amount": -1, "currency": "EUR" }""", false)]
    [InlineData("#/$defs/money", """{ "amount": 1 }""", false)]
    [InlineData("#/$defs/amount", "0", true)]
    [InlineData("#/$defs/amount", "\"184000\"", false)]
    [InlineData("#/$defs/signedAmount", "-2500.75", true)]
    [InlineData("#/$defs/date", "\"2026-03-16\"", true)]
    [InlineData("#/$defs/date", "\"2026-02-30\"", false)]
    [InlineData("#/$defs/date", "\"2026-13-01\"", false)]
    [InlineData("#/$defs/date", "\"16.03.2026\"", false)]
    [InlineData("#/$defs/period", """{ "start": "2026-04-01", "end": "2026-09-30" }""", true)]
    [InlineData("#/$defs/period", """{ "start": "2026-04-01" }""", false)]
    [InlineData("#/$defs/duration", "\"P3M\"", true)]
    [InlineData("#/$defs/duration", "\"P\"", false)]
    [InlineData("#/$defs/text", "\"   \"", false)]
    [InlineData("#/$defs/language", "\"de\"", true)]
    [InlineData("#/$defs/language", "\"fr\"", false)]
    [InlineData("#/$defs/documentStatus", "\"in_review\"", false)]
    [InlineData("#/$defs/email", "\"jana.probe@contoso.example\"", true)]
    [InlineData("#/$defs/email", "\"jana.probe\"", false)]
    [InlineData("#/$defs/documentReference", """{ "type": "msa", "number": "MSA-2026-001" }""", true)]
    [InlineData("#/$defs/documentReference", """{ "type": "contract", "number": "MSA-2026-001" }""", false)]
    public void Definition_AcceptsOnlyWellFormedValues(string reference, string json, bool expectedValid)
    {
        var schema = SchemaCatalog.Instance.BuildReference(reference);
        using var instance = JsonDocument.Parse(json);

        var results = schema.Evaluate(instance.RootElement, SchemaCatalog.EvaluationOptions);

        Assert.Equal(expectedValid, results.IsValid);
    }
}
