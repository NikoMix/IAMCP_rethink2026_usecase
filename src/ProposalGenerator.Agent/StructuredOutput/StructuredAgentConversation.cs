using System.Text;
using ProposalGenerator.Agent.Schemas;

namespace ProposalGenerator.Agent.StructuredOutput;

/// <summary>
/// Sends user messages to the agent and returns only validated replies. An invalid reply triggers a
/// bounded repair loop in the same conversation; when it is exhausted, <see cref="StructuredOutputException"/>
/// reports the failing field paths. Invalid output is never returned.
/// </summary>
public sealed class StructuredAgentConversation
{
    private readonly IAgentResponder _responder;
    private readonly AgentReplyValidator _validator;
    private readonly StructuredOutputOptions _options;

    public StructuredAgentConversation(IAgentResponder responder, DocumentSchemaSet schemas, StructuredOutputOptions? options = null)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        _options = options ?? new StructuredOutputOptions();
        _options.Validate();
        _validator = new AgentReplyValidator(schemas, _options.PlausibilityToolName);
    }

    /// <exception cref="StructuredOutputException">No valid reply after <see cref="StructuredOutputOptions.MaxRepairAttempts"/> corrections.</exception>
    public async Task<AgentTurnResult> SendAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        var toolCalls = new List<ToolCallRecord>();
        var message = userMessage;

        for (var attempt = 1; ; attempt++)
        {
            var reply = await _responder.SendAsync(message, cancellationToken).ConfigureAwait(false);
            toolCalls.AddRange(reply.ToolCalls);

            var result = _validator.Validate(reply.Text, toolCalls);
            if (result.IsValid)
            {
                return new AgentTurnResult
                {
                    Response = result.Response!,
                    Attempts = attempt,
                    ComputedValues = result.ComputedValues,
                    Findings = result.Findings,
                };
            }

            if (attempt > _options.MaxRepairAttempts)
            {
                throw new StructuredOutputException(result.Errors, attempt, reply.Text);
            }

            message = BuildRepairMessage(result.Errors);
        }
    }

    internal string BuildRepairMessage(IReadOnlyList<SchemaValidationError> errors)
    {
        var text = new StringBuilder()
            .AppendLine("Your previous reply failed validation. Fix only these problems and return the complete corrected JSON object, with no text outside the JSON.")
            .AppendLine("If a problem is a mandatory value you do not know, set status to needs_input and ask for it instead of inventing a value.")
            .AppendLine("Problems (JSON Pointer: message):");

        foreach (var error in errors.Take(_options.MaxErrorsInRepairMessage))
        {
            text.Append("- ").AppendLine(error.ToString());
        }

        if (errors.Count > _options.MaxErrorsInRepairMessage)
        {
            text.AppendLine($"- ... and {errors.Count - _options.MaxErrorsInRepairMessage} more.");
        }

        return text.ToString();
    }
}
