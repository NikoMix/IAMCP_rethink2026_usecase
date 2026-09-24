using ProposalGenerator.Agent.Definition;

namespace ProposalGenerator.Agent.Deployment;

public enum DeploymentOutcome
{
    /// <summary>No agent with the name existed; version 1 was created.</summary>
    Created,

    /// <summary>The agent existed with a different definition; a new version was created.</summary>
    Updated,

    /// <summary>The latest version already matches the definition; nothing was written.</summary>
    Unchanged,
}

public sealed record DeploymentResult(DeploymentOutcome Outcome, string AgentName, string Version, string Fingerprint);

/// <summary>Idempotently creates or updates the agent so that its latest version matches the definition.</summary>
public sealed class AgentDeployer
{
    private readonly IFoundryAgentClient _client;

    public AgentDeployer(IFoundryAgentClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<DeploymentResult> DeployAsync(AgentDefinition definition, CancellationToken cancellationToken = default)
    {
        var spec = AgentDeploymentSpec.From(definition);
        var latest = await _client.GetLatestVersionAsync(spec.Name, cancellationToken).ConfigureAwait(false);

        if (latest is not null && Matches(latest, spec))
        {
            return new DeploymentResult(DeploymentOutcome.Unchanged, latest.Name, latest.Version, spec.Fingerprint);
        }

        var created = await _client.CreateVersionAsync(spec, cancellationToken).ConfigureAwait(false);
        var outcome = latest is null ? DeploymentOutcome.Created : DeploymentOutcome.Updated;
        return new DeploymentResult(outcome, created.Name, created.Version, spec.Fingerprint);
    }

    // The fingerprint covers the whole spec; model and instructions are compared as well so that
    // a version edited in the portal (which keeps the old metadata) is detected as drift.
    private static bool Matches(DeployedAgentVersion latest, AgentDeploymentSpec spec) =>
        latest.Metadata.TryGetValue(AgentDeploymentSpec.FingerprintMetadataKey, out var fingerprint)
        && string.Equals(fingerprint, spec.Fingerprint, StringComparison.Ordinal)
        && string.Equals(latest.Model, spec.Model.DeploymentName, StringComparison.Ordinal)
        && string.Equals(Normalize(latest.Instructions), spec.Instructions, StringComparison.Ordinal);

    private static string? Normalize(string? text) => text?.Replace("\r\n", "\n");
}
