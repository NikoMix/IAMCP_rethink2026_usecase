namespace ProposalGenerator.Agent.StructuredOutput;

public sealed class StructuredOutputOptions
{
    public const int DefaultMaxRepairAttempts = 2;
    public const string DefaultPlausibilityToolName = "check_plausibility";

    /// <summary>Corrections requested after an invalid reply before failing. Issue #18 allows at most two.</summary>
    public int MaxRepairAttempts { get; init; } = DefaultMaxRepairAttempts;

    /// <summary>
    /// Function tool that must be called in the same turn before a reply may be
    /// <c>awaiting_confirmation</c> or <c>complete</c>; its <c>computed</c> totals overwrite the model's.
    /// Null disables both rules.
    /// </summary>
    public string? PlausibilityToolName { get; init; } = DefaultPlausibilityToolName;

    /// <summary>Upper bound on validation errors quoted in one repair message.</summary>
    public int MaxErrorsInRepairMessage { get; init; } = 20;

    internal void Validate()
    {
        if (MaxRepairAttempts is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRepairAttempts), MaxRepairAttempts, "Must be between 0 and 10.");
        }

        if (MaxErrorsInRepairMessage < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxErrorsInRepairMessage), MaxErrorsInRepairMessage, "Must be at least 1.");
        }
    }
}
