namespace ProposalGenerator.Agent.Definition;

/// <summary>Raised when an agent definition cannot be loaded; lists every problem found.</summary>
public sealed class AgentDefinitionException : Exception
{
    public AgentDefinitionException(string definitionPath, IReadOnlyList<string> errors)
        : base(BuildMessage(definitionPath, errors))
    {
        DefinitionPath = definitionPath;
        Errors = errors;
    }

    public string DefinitionPath { get; }

    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(string path, IReadOnlyList<string> errors) =>
        $"Agent definition '{path}' is invalid:{Environment.NewLine}- " + string.Join(Environment.NewLine + "- ", errors);
}
