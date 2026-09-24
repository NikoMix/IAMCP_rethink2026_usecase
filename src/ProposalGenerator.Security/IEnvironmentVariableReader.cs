namespace ProposalGenerator.Security;

/// <summary>
/// Reads process environment variables. Abstracted so credential selection is testable without mutating the process environment.
/// </summary>
public interface IEnvironmentVariableReader
{
    /// <summary>
    /// Returns the value of the environment variable, or <see langword="null"/> when it is not set.
    /// </summary>
    string? Get(string name);
}

internal sealed class ProcessEnvironmentVariableReader : IEnvironmentVariableReader
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}
