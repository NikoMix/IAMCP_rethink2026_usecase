using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProposalGenerator.Agent.Definition;

namespace ProposalGenerator.Agent.Deployment;

/// <summary>Desired state of the agent in the Foundry Agent Service, independent of the SDK types.</summary>
public sealed class AgentDeploymentSpec
{
    public const string FingerprintMetadataKey = "definition-sha256";
    public const string DefinitionVersionMetadataKey = "definition-version";
    public const string ManagedByMetadataKey = "managed-by";
    public const string ManagedByValue = "ProposalGenerator.Agent";

    private AgentDeploymentSpec(AgentDefinition definition, JsonObject canonical)
    {
        Name = definition.Name;
        Description = definition.Description;
        Model = definition.Model;
        Instructions = definition.Instructions;
        Tools = definition.Tools;
        ResponseFormatName = definition.ResponseFormat.Name;
        ResponseFormatStrict = definition.ResponseFormat.Strict;
        ResponseFormatSchema = definition.ResponseFormat.ModelSchema.ToJsonString();
        CanonicalJson = canonical.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        Fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToJsonString())));
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FingerprintMetadataKey] = Fingerprint,
            [DefinitionVersionMetadataKey] = definition.Version,
            [ManagedByMetadataKey] = ManagedByValue,
        };
    }

    public string Name { get; }
    public string Description { get; }
    public AgentModelSettings Model { get; }
    public string Instructions { get; }
    public IReadOnlyList<AgentToolDefinition> Tools { get; }
    public string ResponseFormatName { get; }
    public bool ResponseFormatStrict { get; }

    /// <summary>Self-contained JSON schema text for the response format.</summary>
    public string ResponseFormatSchema { get; }

    /// <summary>SHA-256 over the canonical JSON of everything that is deployed.</summary>
    public string Fingerprint { get; }

    /// <summary>Indented canonical JSON of the desired state, for dry runs and reviews.</summary>
    public string CanonicalJson { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public static AgentDeploymentSpec From(AgentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var tools = new JsonArray();
        foreach (var tool in definition.Tools)
        {
            tools.Add(tool switch
            {
                FileSearchToolDefinition fileSearch => new JsonObject
                {
                    ["type"] = fileSearch.Type,
                    ["vectorStoreIds"] = new JsonArray(fileSearch.VectorStoreIds.Select(id => (JsonNode?)id).ToArray()),
                    ["maxResults"] = fileSearch.MaxResults,
                },
                FunctionToolDefinition function => new JsonObject
                {
                    ["type"] = function.Type,
                    ["name"] = function.Name,
                    ["description"] = function.Description,
                    ["strict"] = function.Strict,
                    ["parameters"] = function.Parameters.DeepClone(),
                },
                _ => throw new NotSupportedException($"Tool type '{tool.Type}' is not supported."),
            });
        }

        var canonical = new JsonObject
        {
            ["name"] = definition.Name,
            ["description"] = definition.Description,
            ["model"] = definition.Model.DeploymentName,
            ["temperature"] = definition.Model.Temperature,
            ["topP"] = definition.Model.TopP,
            ["instructions"] = definition.Instructions,
            ["tools"] = tools,
            ["responseFormat"] = new JsonObject
            {
                ["name"] = definition.ResponseFormat.Name,
                ["strict"] = definition.ResponseFormat.Strict,
                ["schema"] = definition.ResponseFormat.ModelSchema.DeepClone(),
            },
        };

        return new AgentDeploymentSpec(definition, canonical);
    }
}
