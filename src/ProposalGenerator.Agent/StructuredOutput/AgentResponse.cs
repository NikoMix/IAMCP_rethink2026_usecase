using System.Text.Json.Nodes;

namespace ProposalGenerator.Agent.StructuredOutput;

public enum AgentResponseStatus
{
    NeedsInput,
    AwaitingConfirmation,
    Complete,
}

/// <summary>A validated agent reply (see <c>agent/response-envelope.schema.json</c>).</summary>
public sealed class AgentResponse
{
    public required AgentResponseStatus Status { get; init; }
    public required string Language { get; init; }
    public required string Message { get; init; }
    public string? DocumentType { get; init; }
    public required IReadOnlyList<string> Questions { get; init; }
    public required IReadOnlyList<string> MissingFields { get; init; }

    /// <summary>
    /// Document data. Valid against the schema of <see cref="DocumentType"/> when the status is
    /// <see cref="AgentResponseStatus.AwaitingConfirmation"/> or <see cref="AgentResponseStatus.Complete"/>,
    /// with totals taken from <c>check_plausibility</c>.
    /// </summary>
    public JsonObject? Document { get; init; }
}

/// <summary>A total that was replaced with the value computed by <c>check_plausibility</c>.</summary>
/// <param name="Path">JSON Pointer inside the reply, for example <c>/document/pricing/total</c>.</param>
public sealed record AppliedComputedValue(string Path, JsonNode? ModelValue, decimal ToolValue);

public sealed record PlausibilityFinding(string Code, string Severity, string Path, string Message);

public sealed class AgentTurnResult
{
    public required AgentResponse Response { get; init; }

    /// <summary>Number of agent replies used, including the first one.</summary>
    public required int Attempts { get; init; }

    public int RepairAttempts => Attempts - 1;

    public required IReadOnlyList<AppliedComputedValue> ComputedValues { get; init; }

    /// <summary>Findings of the last <c>check_plausibility</c> call in this turn, to show before generation.</summary>
    public required IReadOnlyList<PlausibilityFinding> Findings { get; init; }
}
