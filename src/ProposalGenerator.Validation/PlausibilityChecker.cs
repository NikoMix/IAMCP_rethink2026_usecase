using System.Text.Json;
using ProposalGenerator.Validation.RateCards;
using ProposalGenerator.Validation.Registry;
using ProposalGenerator.Validation.Rules;

namespace ProposalGenerator.Validation;

/// <summary>Configuration of the plausibility check.</summary>
public sealed class PlausibilityCheckerOptions
{
    /// <summary>Field paths the rules read; defaults follow the JSON Schemas.</summary>
    public PlausibilityFieldPaths FieldPaths { get; init; } = new();

    /// <summary>Rate cards to compare daily rates with. When null, rates are reported as unverified (info).</summary>
    public IRateCardProvider? RateCardProvider { get; init; }

    /// <summary>Registry of predecessor documents. When null, predecessors are reported as unverified (info).</summary>
    public IDocumentRegistry? DocumentRegistry { get; init; }
}

/// <summary>
/// Runs all deterministic plausibility rules against a document. The check never modifies the document and never
/// throws for content problems; every problem becomes a <see cref="PlausibilityFinding"/>.
/// </summary>
public sealed class PlausibilityChecker
{
    internal static IReadOnlyList<IPlausibilityRule> Rules { get; } =
    [
        new DocumentTypeRule(),
        new DateFormatRule(),
        new NumberFormatRule(),
        new NonNegativeRule(),
        new ProjectPeriodRule(),
        new DeadlineOrderRule(),
        new WithinProjectPeriodRule(),
        new CurrencyRule(),
        new RateCardArithmeticRule(),
        new PaymentScheduleRule(),
        new EvaluationWeightsRule(),
        new RateCardComplianceRule(),
        new ChangeRequestArithmeticRule(),
        new ReferenceConsistencyRule(),
        new PredecessorRule(),
    ];

    private readonly PlausibilityCheckerOptions _options;

    /// <summary>Creates a checker.</summary>
    /// <exception cref="ArgumentException">A configured field path is malformed.</exception>
    public PlausibilityChecker(PlausibilityCheckerOptions? options = null)
    {
        _options = options ?? new PlausibilityCheckerOptions();
        _options.FieldPaths.Validate();
    }

    /// <summary>Checks a document of the given type.</summary>
    /// <param name="documentType">Type the document is checked as.</param>
    /// <param name="document">Document JSON in the shape of <c>schemas/&lt;documentType&gt;.schema.json</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<PlausibilityResult> CheckAsync(DocumentType documentType, JsonElement document, CancellationToken cancellationToken = default)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return new PlausibilityResult(
                [new PlausibilityFinding(FindingCodes.ArgumentsInvalid, FindingSeverity.Error, "document", "Das Dokument fehlt oder ist kein JSON-Objekt.")],
                new Dictionary<string, decimal>());
        }

        var rateCards = _options.RateCardProvider is null
            ? null
            : await _options.RateCardProvider.GetRateCardsAsync(cancellationToken).ConfigureAwait(false);
        var context = new RuleContext(document, documentType, _options.FieldPaths, rateCards, _options.DocumentRegistry);
        foreach (var rule in Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await rule.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return new PlausibilityResult(
            context.Findings.OrderBy(f => f.Severity).ToArray(),
            context.Computed.OrderBy(c => c.Key, StringComparer.Ordinal).ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal));
    }
}
