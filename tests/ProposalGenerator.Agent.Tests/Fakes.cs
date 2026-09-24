using ProposalGenerator.Agent.Deployment;
using ProposalGenerator.Agent.StructuredOutput;

namespace ProposalGenerator.Agent.Tests;

/// <summary>In-memory Foundry Agent Service: agents by name, each with numbered versions.</summary>
internal sealed class FakeFoundryAgentClient : IFoundryAgentClient
{
    private readonly Dictionary<string, List<DeployedAgentVersion>> _agents = new(StringComparer.Ordinal);

    public int CreateCalls { get; private set; }

    public int AgentCount => _agents.Count;

    public AgentDeploymentSpec? LastSpec { get; private set; }

    public IReadOnlyList<DeployedAgentVersion> Versions(string name) => _agents.TryGetValue(name, out var v) ? v : [];

    public Task<DeployedAgentVersion?> GetLatestVersionAsync(string agentName, CancellationToken cancellationToken) =>
        Task.FromResult(_agents.TryGetValue(agentName, out var versions) ? versions[^1] : null);

    public Task<DeployedAgentVersion> CreateVersionAsync(AgentDeploymentSpec spec, CancellationToken cancellationToken)
    {
        CreateCalls++;
        LastSpec = spec;
        if (!_agents.TryGetValue(spec.Name, out var versions))
        {
            versions = [];
            _agents[spec.Name] = versions;
        }

        var version = new DeployedAgentVersion(
            spec.Name,
            (versions.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            new Dictionary<string, string>(spec.Metadata),
            spec.Model.DeploymentName,
            spec.Instructions);
        versions.Add(version);
        return Task.FromResult(version);
    }

    /// <summary>Simulates an edit in the portal that keeps the old metadata.</summary>
    public void EditLatestInstructions(string name, string instructions)
    {
        var versions = _agents[name];
        versions[^1] = versions[^1] with { Instructions = instructions };
    }
}

/// <summary>Returns scripted replies in order and records every message it receives.</summary>
internal sealed class ScriptedResponder : IAgentResponder
{
    private readonly Queue<AgentReply> _replies;

    public ScriptedResponder(params AgentReply[] replies)
    {
        _replies = new Queue<AgentReply>(replies);
    }

    public List<string> Messages { get; } = [];

    public Task<AgentReply> SendAsync(string message, CancellationToken cancellationToken)
    {
        Messages.Add(message);
        if (_replies.Count == 0)
        {
            throw new InvalidOperationException("The scripted responder has no reply left; the loop asked more often than expected.");
        }

        return Task.FromResult(_replies.Dequeue());
    }
}
