using System.Text.Json.Nodes;
using ProposalGenerator.Agent.Schemas;

namespace ProposalGenerator.Agent.Definition;

/// <summary>Validated, fully resolved agent definition loaded from <c>agent/proposal-agent.yaml</c>.</summary>
public sealed class AgentDefinition
{
    public required string SourcePath { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required AgentModelSettings Model { get; init; }

    /// <summary>System instructions with line endings normalised to LF.</summary>
    public required string Instructions { get; init; }

    public required IReadOnlyList<AgentToolDefinition> Tools { get; init; }
    public required ResponseFormatDefinition ResponseFormat { get; init; }
}

public sealed record AgentModelSettings(string DeploymentName, float? Temperature, float? TopP);

public abstract class AgentToolDefinition
{
    public abstract string Type { get; }
}

public sealed class FileSearchToolDefinition : AgentToolDefinition
{
    public override string Type => "file_search";
    public required IReadOnlyList<string> VectorStoreIds { get; init; }
    public int? MaxResults { get; init; }
}

public sealed class FunctionToolDefinition : AgentToolDefinition
{
    public override string Type => "function";
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonObject Parameters { get; init; }
    public bool Strict { get; init; }
}

public sealed class ResponseFormatDefinition
{
    public required string Name { get; init; }
    public bool Strict { get; init; }
    public required DocumentSchemaSet Schemas { get; init; }

    /// <summary>Self-contained schema sent to the model as <c>text.format</c>.</summary>
    public required JsonObject ModelSchema { get; init; }
}
