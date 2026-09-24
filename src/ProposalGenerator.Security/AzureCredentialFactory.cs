using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace ProposalGenerator.Security;

/// <summary>
/// The concrete credential type the factory builds.
/// </summary>
public enum AzureCredentialKind
{
    /// <summary><c>DefaultAzureCredential</c> without managed identity, for local development.</summary>
    DeveloperCredentialChain = 0,

    /// <summary><c>ManagedIdentityCredential</c> using the system-assigned identity.</summary>
    SystemAssignedManagedIdentity = 1,

    /// <summary><c>ManagedIdentityCredential</c> using a user-assigned identity identified by client ID.</summary>
    UserAssignedManagedIdentity = 2,
}

/// <summary>
/// The resolved credential decision, exposed so the selection can be logged and tested without acquiring a token.
/// </summary>
/// <param name="Kind">Credential type to build.</param>
/// <param name="ManagedIdentityClientId">Client ID of the user-assigned identity, if any.</param>
/// <param name="TenantId">Tenant for the developer credential chain, if configured.</param>
public sealed record AzureCredentialPlan(AzureCredentialKind Kind, string? ManagedIdentityClientId, string? TenantId);

/// <summary>
/// Creates the <see cref="TokenCredential"/> the application uses for every Azure call (Foundry, Storage, Monitor).
/// </summary>
public interface IAzureCredentialFactory
{
    /// <summary>Resolves which credential will be built, without building it.</summary>
    AzureCredentialPlan Resolve();

    /// <summary>Builds the credential described by <see cref="Resolve"/>.</summary>
    TokenCredential Create();
}

/// <summary>
/// Default <see cref="IAzureCredentialFactory"/>: <c>DefaultAzureCredential</c> locally, <c>ManagedIdentityCredential</c> in Azure.
/// </summary>
public sealed class AzureCredentialFactory : IAzureCredentialFactory
{
    /// <summary>Standard client ID variable read by Azure SDKs and set on Container Apps for the app identity.</summary>
    public const string ClientIdEnvironmentVariable = "AZURE_CLIENT_ID";

    // Set by the managed identity endpoint of Azure Container Apps and App Service.
    internal static readonly string[] ManagedIdentityEndpointVariables = ["IDENTITY_ENDPOINT", "MSI_ENDPOINT"];

    private readonly AzureCredentialOptions _options;
    private readonly IEnvironmentVariableReader _environment;

    /// <summary>Creates the factory.</summary>
    public AzureCredentialFactory(IOptions<AzureCredentialOptions> options, IEnvironmentVariableReader environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        _options = options.Value;
        _environment = environment;
    }

    /// <inheritdoc />
    public AzureCredentialPlan Resolve()
    {
        var mode = _options.Mode;
        if (!Enum.IsDefined(mode))
        {
            throw new InvalidOperationException($"Unsupported {nameof(AzureCredentialOptions.Mode)} '{mode}'.");
        }

        var useManagedIdentity = mode switch
        {
            AzureCredentialMode.ManagedIdentity => true,
            AzureCredentialMode.Development => false,
            _ => ManagedIdentityEndpointVariables.Any(name => !string.IsNullOrWhiteSpace(_environment.Get(name))),
        };

        var clientId = Normalize(_options.ManagedIdentityClientId) ?? Normalize(_environment.Get(ClientIdEnvironmentVariable));
        EnsureGuid(clientId, nameof(AzureCredentialOptions.ManagedIdentityClientId));

        if (useManagedIdentity)
        {
            return clientId is null
                ? new AzureCredentialPlan(AzureCredentialKind.SystemAssignedManagedIdentity, null, null)
                : new AzureCredentialPlan(AzureCredentialKind.UserAssignedManagedIdentity, clientId, null);
        }

        var tenantId = Normalize(_options.TenantId);
        EnsureGuid(tenantId, nameof(AzureCredentialOptions.TenantId));
        return new AzureCredentialPlan(AzureCredentialKind.DeveloperCredentialChain, null, tenantId);
    }

    /// <inheritdoc />
    public TokenCredential Create() => Create(Resolve());

    internal static TokenCredential Create(AzureCredentialPlan plan) => plan.Kind switch
    {
        AzureCredentialKind.UserAssignedManagedIdentity =>
            new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(plan.ManagedIdentityClientId!)),
        AzureCredentialKind.SystemAssignedManagedIdentity =>
            new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
        AzureCredentialKind.DeveloperCredentialChain => new DefaultAzureCredential(CreateDeveloperOptions(plan.TenantId)),
        _ => throw new InvalidOperationException($"Unsupported credential kind '{plan.Kind}'."),
    };

    internal static DefaultAzureCredentialOptions CreateDeveloperOptions(string? tenantId)
    {
        var options = new DefaultAzureCredentialOptions
        {
            // Locally there is no managed identity; probing IMDS only adds start-up latency.
            ExcludeManagedIdentityCredential = true,
            ExcludeWorkloadIdentityCredential = true,
            ExcludeInteractiveBrowserCredential = true,
        };

        if (tenantId is not null)
        {
            options.TenantId = tenantId;
        }

        return options;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureGuid(string? value, string settingName)
    {
        if (value is not null && !Guid.TryParse(value, out _))
        {
            throw new InvalidOperationException($"{settingName} must be a GUID when set.");
        }
    }
}
