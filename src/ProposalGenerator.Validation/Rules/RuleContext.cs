using System.Text.Json;
using ProposalGenerator.Validation.Internal;
using ProposalGenerator.Validation.RateCards;
using ProposalGenerator.Validation.Registry;

namespace ProposalGenerator.Validation.Rules;

/// <summary>A deterministic plausibility rule.</summary>
internal interface IPlausibilityRule
{
    /// <summary>Codes this rule can report.</summary>
    IReadOnlyCollection<string> Codes { get; }

    ValueTask EvaluateAsync(RuleContext context, CancellationToken cancellationToken);
}

/// <summary>State shared by the rules of one check: the document, configuration, and the collected results.</summary>
internal sealed class RuleContext(
    JsonElement document,
    DocumentType documentType,
    PlausibilityFieldPaths paths,
    IReadOnlyList<RateCard>? rateCards,
    IDocumentRegistry? registry)
{
    private readonly List<PlausibilityFinding> _findings = [];
    private readonly Dictionary<string, decimal> _computed = new(StringComparer.Ordinal);

    public JsonElement Document { get; } = document;

    public DocumentType DocumentType { get; } = documentType;

    public PlausibilityFieldPaths Paths { get; } = paths;

    /// <summary>Configured rate cards, or null when no provider is configured.</summary>
    public IReadOnlyList<RateCard>? RateCards { get; } = rateCards;

    /// <summary>Configured registry, or null when none is configured.</summary>
    public IDocumentRegistry? Registry { get; } = registry;

    public IReadOnlyList<PlausibilityFinding> Findings => _findings;

    public IReadOnlyDictionary<string, decimal> Computed => _computed;

    public Located? Find(string path) => JsonNavigator.Find(Document, JsonNavigator.RootPath, path);

    public IReadOnlyList<Located> Items(string path) => JsonNavigator.Items(Document, JsonNavigator.RootPath, path);

    /// <summary>JSONPath of a configured path, used for findings about fields that are absent.</summary>
    public static string PathOf(string path) => $"{JsonNavigator.RootPath}.{path}";

    public void Report(string code, FindingSeverity severity, string path, string message) =>
        _findings.Add(new PlausibilityFinding(code, severity, path, message));

    public void SetComputed(string path, decimal value) => _computed[path] = value;
}
