using System.Text.Json.Serialization;

namespace ProposalGenerator.Validation;

/// <summary>Severity of a plausibility finding.</summary>
public enum FindingSeverity
{
    /// <summary>The document is wrong and must not be generated as is.</summary>
    [JsonStringEnumMemberName("error")]
    Error,

    /// <summary>The document is probably wrong; the user must confirm or correct it.</summary>
    [JsonStringEnumMemberName("warning")]
    Warning,

    /// <summary>A hint, or a check that could not be performed.</summary>
    [JsonStringEnumMemberName("info")]
    Info,
}

/// <summary>A single result of the plausibility check.</summary>
/// <param name="Code">Stable, machine-readable code from <see cref="FindingCodes"/>.</param>
/// <param name="Severity">Severity of the finding.</param>
/// <param name="Path">
/// JSONPath of the affected field relative to the document root, for example
/// <c>$.pricing.payment_schedule[1].amount</c>. Findings about tool arguments use the argument name instead.
/// </param>
/// <param name="Message">User-facing message in German.</param>
public sealed record PlausibilityFinding(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("severity"), JsonConverter(typeof(JsonStringEnumConverter<FindingSeverity>))] FindingSeverity Severity,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("message")] string Message);

/// <summary>Result of the <c>check_plausibility</c> tool.</summary>
/// <param name="Findings">All findings, errors first, then warnings, then infos; otherwise in rule order.</param>
/// <param name="Computed">
/// Values derived deterministically by the tool, keyed by the JSONPath of the document field they belong to,
/// for example <c>$.pricing.rate_card[0].subtotal</c>. The agent uses these values instead of calculating itself.
/// </param>
public sealed record PlausibilityResult(
    [property: JsonPropertyName("findings")] IReadOnlyList<PlausibilityFinding> Findings,
    [property: JsonPropertyName("computed")] IReadOnlyDictionary<string, decimal> Computed)
{
    /// <summary>True when at least one finding has severity <see cref="FindingSeverity.Error"/>.</summary>
    [JsonIgnore]
    public bool HasErrors => Findings.Any(f => f.Severity == FindingSeverity.Error);
}
