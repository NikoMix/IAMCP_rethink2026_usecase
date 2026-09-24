namespace ProposalGenerator.Agent.Deployment;

/// <summary>
/// Minimal port to the Foundry Agent Service. <see cref="FoundryAgentClient"/> implements it with the
/// official SDK; tests use an in-memory fake.
/// </summary>
public interface IFoundryAgentClient
{
    /// <summary>Returns the latest version of the named agent, or null when no agent with that name exists.</summary>
    Task<DeployedAgentVersion?> GetLatestVersionAsync(string agentName, CancellationToken cancellationToken);

    /// <summary>Creates the agent, or a new version of an existing agent with the same name.</summary>
    Task<DeployedAgentVersion> CreateVersionAsync(AgentDeploymentSpec spec, CancellationToken cancellationToken);
}

/// <summary>What the service reports about one agent version.</summary>
public sealed record DeployedAgentVersion(
    string Name,
    string Version,
    IReadOnlyDictionary<string, string> Metadata,
    string? Model,
    string? Instructions);
