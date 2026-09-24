namespace ProposalGenerator.Agent.Definition;

// Mutable shapes for YamlDotNet. Unknown keys fail deserialisation, so typos are not ignored.
internal sealed class AgentDefinitionDocument
{
    public int? SchemaVersion { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public ModelDocument? Model { get; set; }
    public string? Instructions { get; set; }
    public List<ToolDocument>? Tools { get; set; }
    public ResponseFormatDocument? ResponseFormat { get; set; }
}

internal sealed class ModelDocument
{
    public string? Deployment { get; set; }
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
}

internal sealed class ToolDocument
{
    public string? Type { get; set; }
    public List<string>? VectorStoreIds { get; set; }
    public int? MaxResults { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Parameters { get; set; }
    public bool? Strict { get; set; }
}

internal sealed class ResponseFormatDocument
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public bool? Strict { get; set; }
    public string? Envelope { get; set; }
    public string? SchemaDirectory { get; set; }
    public List<string>? DocumentTypes { get; set; }
}
