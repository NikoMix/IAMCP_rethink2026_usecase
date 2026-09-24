using System.ClientModel;
using Azure.AI.Projects.Agents;
using OpenAI.Responses;
using ProposalGenerator.Agent.Definition;

namespace ProposalGenerator.Agent.Deployment;

/// <summary>
/// <see cref="IFoundryAgentClient"/> backed by <c>Azure.AI.Projects.Agents</c> (Foundry Agent Service, prompt agents).
/// Not covered by offline tests; keep it a thin mapping layer.
/// </summary>
public sealed class FoundryAgentClient : IFoundryAgentClient
{
    private readonly AgentAdministrationClient _client;

    /// <param name="projectEndpoint">Foundry project endpoint, for example <c>https://&lt;account&gt;.services.ai.azure.com/api/projects/&lt;project&gt;</c>.</param>
    /// <param name="credential">Keyless credential, typically <c>DefaultAzureCredential</c>.</param>
    public FoundryAgentClient(Uri projectEndpoint, AuthenticationTokenProvider credential)
    {
        ArgumentNullException.ThrowIfNull(projectEndpoint);
        ArgumentNullException.ThrowIfNull(credential);
        _client = new AgentAdministrationClient(projectEndpoint, credential);
    }

    public async Task<DeployedAgentVersion?> GetLatestVersionAsync(string agentName, CancellationToken cancellationToken)
    {
        ProjectsAgentRecord record;
        try
        {
            record = (await _client.GetAgentAsync(agentName, cancellationToken).ConfigureAwait(false)).Value;
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            return null;
        }

        return Map(record.GetLatestVersion());
    }

    public async Task<DeployedAgentVersion> CreateVersionAsync(AgentDeploymentSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);

#pragma warning disable OPENAI001 // Responses tool and text-format types are marked experimental in the OpenAI SDK.
        var definition = new DeclarativeAgentDefinition(spec.Model.DeploymentName)
        {
            Instructions = spec.Instructions,
            Temperature = spec.Model.Temperature,
            TopP = spec.Model.TopP,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    spec.ResponseFormatName,
                    BinaryData.FromString(spec.ResponseFormatSchema),
                    jsonSchemaFormatDescription: null,
                    jsonSchemaIsStrict: spec.ResponseFormatStrict),
            },
        };

        foreach (var tool in spec.Tools)
        {
            definition.Tools.Add(tool switch
            {
                FileSearchToolDefinition fileSearch => ResponseTool.CreateFileSearchTool(
                    fileSearch.VectorStoreIds, fileSearch.MaxResults, rankingOptions: null, filters: null),
                FunctionToolDefinition function => ResponseTool.CreateFunctionTool(
                    function.Name,
                    BinaryData.FromString(function.Parameters.ToJsonString()),
                    function.Strict,
                    function.Description),
                _ => throw new NotSupportedException($"Tool type '{tool.Type}' is not supported."),
            });
        }
#pragma warning restore OPENAI001

        var options = new ProjectsAgentVersionCreationOptions(definition) { Description = spec.Description };
        foreach (var (key, value) in spec.Metadata)
        {
            options.Metadata[key] = value;
        }

        var version = (await _client.CreateAgentVersionAsync(spec.Name, options, foundryFeatures: null, cancellationToken).ConfigureAwait(false)).Value;
        return Map(version);
    }

    private static DeployedAgentVersion Map(ProjectsAgentVersion version)
    {
        var declarative = version.Definition as DeclarativeAgentDefinition;
        return new DeployedAgentVersion(
            version.Name,
            version.Version,
            new Dictionary<string, string>(version.Metadata, StringComparer.Ordinal),
            declarative?.Model,
            declarative?.Instructions);
    }
}
