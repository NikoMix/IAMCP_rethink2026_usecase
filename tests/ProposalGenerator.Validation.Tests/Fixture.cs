using System.Text.Json;
using System.Text.Json.Nodes;
using ProposalGenerator.Validation.RateCards;
using ProposalGenerator.Validation.Registry;

namespace ProposalGenerator.Validation.Tests;

/// <summary>
/// Loads the fixtures and runs checks. The <c>*.valid.json</c> fixtures are copies of
/// <c>schemas/examples/valid/*.example.json</c> from the Data model branch (commit 91d1219), without the relative
/// <c>$schema</c> pointer. Refresh them when the schemas change.
/// </summary>
internal static class Fixture
{
    private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string RateCardPath { get; } = Path.Combine(Directory, "knowledge", "rate-card.json");

    public static RateCard RateCard { get; } = RateCardLoader.LoadFileAsync(RateCardPath).GetAwaiter().GetResult();

    public static JsonObject Load(string documentType) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Directory, $"{documentType}.valid.json")))!.AsObject();

    public static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);

    /// <summary>Registry with the MSA, SOW and RFI examples, which the other examples reference.</summary>
    public static InMemoryDocumentRegistry Registry() => new InMemoryDocumentRegistry()
        .Add(DocumentType.Msa, "MSA-2026-001", "1.0", ToElement(Load("msa")))
        .Add(DocumentType.Sow, "SOW-2026-001", "1.0", ToElement(Load("sow")))
        .Add(DocumentType.Rfi, "RFI-2026-003", "1.0", ToElement(Load("rfi")));

    /// <summary>Options with the knowledge rate card and the example registry.</summary>
    public static PlausibilityCheckerOptions FullOptions(IDocumentRegistry? registry = null) => new()
    {
        RateCardProvider = new StaticRateCardProvider([RateCard]),
        DocumentRegistry = registry ?? Registry(),
    };

    public static Task<PlausibilityResult> CheckAsync(DocumentType type, JsonNode document, PlausibilityCheckerOptions? options = null) =>
        new PlausibilityChecker(options ?? FullOptions()).CheckAsync(type, ToElement(document));

    public static async Task<PlausibilityResult> CheckAsync(DocumentType type, Action<JsonObject> mutate, PlausibilityCheckerOptions? options = null)
    {
        var document = Load(type.ToName());
        mutate(document);
        return await CheckAsync(type, document, options);
    }

    public static JsonObject Obj(this JsonNode node, string name) => node[name]!.AsObject();

    public static JsonObject Item(this JsonNode node, string name, int index) => node[name]!.AsArray()[index]!.AsObject();
}

internal static class FindingAssert
{
    /// <summary>Asserts that no finding has the code.</summary>
    public static void None(PlausibilityResult result, string code) =>
        Assert.True(result.Findings.All(f => f.Code != code), $"Unexpected {code}: {Describe(result)}");

    /// <summary>Asserts exactly one finding with the code and returns it after checking severity and path.</summary>
    public static PlausibilityFinding Single(PlausibilityResult result, string code, FindingSeverity severity, string path)
    {
        var matches = result.Findings.Where(f => f.Code == code).ToArray();
        Assert.True(matches.Length == 1, $"Expected exactly one {code}, got {matches.Length}: {Describe(result)}");
        var finding = matches[0];
        Assert.Equal(severity, finding.Severity);
        Assert.Equal(path, finding.Path);
        Assert.False(string.IsNullOrWhiteSpace(finding.Message));
        return finding;
    }

    /// <summary>Asserts that the result has no findings at all.</summary>
    public static void Clean(PlausibilityResult result) =>
        Assert.True(result.Findings.Count == 0, $"Expected no findings: {Describe(result)}");

    private static string Describe(PlausibilityResult result) =>
        result.Findings.Count == 0
            ? "(no findings)"
            : string.Join(" | ", result.Findings.Select(f => $"{f.Code} {f.Severity} {f.Path}: {f.Message}"));
}
