namespace ProposalGenerator.Security;

/// <summary>
/// Configuration for the Azure credential. Bound from the <see cref="SectionName"/> configuration section.
/// Contains identifiers only; secrets are never read from configuration.
/// </summary>
public sealed class AzureCredentialOptions
{
    /// <summary>
    /// Configuration section that binds to these options.
    /// </summary>
    public const string SectionName = "Azure:Credential";

    /// <summary>
    /// Credential selection strategy. Defaults to <see cref="AzureCredentialMode.Auto"/>.
    /// </summary>
    public AzureCredentialMode Mode { get; set; } = AzureCredentialMode.Auto;

    /// <summary>
    /// Client ID of the user-assigned managed identity. When empty, the <c>AZURE_CLIENT_ID</c>
    /// environment variable is used; when both are empty, the system-assigned identity is used.
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }

    /// <summary>
    /// Optional Entra ID tenant for the local developer credential chain, for developers with access to several tenants.
    /// </summary>
    public string? TenantId { get; set; }
}
