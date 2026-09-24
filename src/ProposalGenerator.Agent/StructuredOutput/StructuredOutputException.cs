using ProposalGenerator.Agent.Schemas;

namespace ProposalGenerator.Agent.StructuredOutput;

/// <summary>The agent did not return a valid reply within the allowed repair attempts.</summary>
public sealed class StructuredOutputException : Exception
{
    public StructuredOutputException(IReadOnlyList<SchemaValidationError> errors, int attempts, string lastOutput)
        : base(BuildMessage(errors, attempts))
    {
        Errors = errors;
        Attempts = attempts;
        LastOutput = lastOutput;
    }

    /// <summary>Validation errors of the last reply, each with the JSON Pointer of the failing field.</summary>
    public IReadOnlyList<SchemaValidationError> Errors { get; }

    public int Attempts { get; }

    public string LastOutput { get; }

    /// <summary>Distinct failing field paths of the last reply.</summary>
    public IReadOnlyList<string> FieldPaths => Errors.Select(e => e.Path.Length == 0 ? "/" : e.Path).Distinct().ToArray();

    private static string BuildMessage(IReadOnlyList<SchemaValidationError> errors, int attempts) =>
        $"The agent reply was still invalid after {attempts} attempt(s). Failing fields:{Environment.NewLine}- "
        + string.Join(Environment.NewLine + "- ", errors);
}
