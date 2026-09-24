namespace ProposalGenerator.Security;

/// <summary>
/// Selects which Entra ID credential the application uses to call Azure services.
/// </summary>
public enum AzureCredentialMode
{
    /// <summary>
    /// Use a managed identity when the process runs on an Azure host that exposes one
    /// (for example Azure Container Apps), otherwise use the local developer credential chain.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Always use <c>DefaultAzureCredential</c> with managed identity excluded (Azure CLI, azd, Visual Studio, ...).
    /// </summary>
    Development = 1,

    /// <summary>
    /// Always use <c>ManagedIdentityCredential</c>. User-assigned when a client ID is configured, otherwise system-assigned.
    /// </summary>
    ManagedIdentity = 2,
}
