namespace ProposalGenerator.Agent.Schemas;

/// <summary>One schema violation, located by a JSON Pointer into the agent reply.</summary>
public sealed record SchemaValidationError(string Path, string Keyword, string Message)
{
    public override string ToString() => $"{(Path.Length == 0 ? "/" : Path)}: {Message}";
}
