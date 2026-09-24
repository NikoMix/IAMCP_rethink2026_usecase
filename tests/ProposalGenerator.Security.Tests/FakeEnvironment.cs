namespace ProposalGenerator.Security.Tests;

internal sealed class FakeEnvironment : Dictionary<string, string?>, IEnvironmentVariableReader
{
    public FakeEnvironment()
        : base(StringComparer.Ordinal)
    {
    }

    public string? Get(string name) => TryGetValue(name, out var value) ? value : null;
}
