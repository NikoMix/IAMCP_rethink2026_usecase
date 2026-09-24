namespace ProposalGenerator.Agent.StructuredOutput;

/// <summary>
/// One conversation (thread) with the deployed agent. Implementations send a user message, run any
/// tool calls, and return the agent's final text. Follow-up messages continue the same conversation.
/// </summary>
public interface IAgentResponder
{
    Task<AgentReply> SendAsync(string message, CancellationToken cancellationToken);
}

/// <summary>The agent's final text plus the function tool calls it made while producing it.</summary>
public sealed record AgentReply(string Text, IReadOnlyList<ToolCallRecord> ToolCalls)
{
    public AgentReply(string text)
        : this(text, [])
    {
    }
}

/// <param name="Arguments">JSON arguments the model passed to the tool.</param>
/// <param name="Output">JSON the tool handler returned to the model.</param>
public sealed record ToolCallRecord(string Name, string Arguments, string Output);
