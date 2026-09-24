using Azure.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Identity.Web;

namespace ProposalGenerator.Security;

/// <summary>
/// Registers keyless Azure access and Entra ID sign-in for the proposal generator hosts.
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AzureCredentialOptions"/>, <see cref="IAzureCredentialFactory"/> and a singleton
    /// <see cref="TokenCredential"/> for Azure SDK clients.
    /// </summary>
    public static IServiceCollection AddProposalGeneratorAzureCredential(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AzureCredentialOptions>()
            .Bind(configuration.GetSection(AzureCredentialOptions.SectionName))
            .Validate(options => Enum.IsDefined(options.Mode), $"{AzureCredentialOptions.SectionName}:Mode is not a supported value.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ManagedIdentityClientId) || Guid.TryParse(options.ManagedIdentityClientId, out _),
                $"{AzureCredentialOptions.SectionName}:ManagedIdentityClientId must be a GUID when set.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.TenantId) || Guid.TryParse(options.TenantId, out _),
                $"{AzureCredentialOptions.SectionName}:TenantId must be a GUID when set.")
            .ValidateOnStart();

        services.TryAddSingleton<IEnvironmentVariableReader, ProcessEnvironmentVariableReader>();
        services.TryAddSingleton<IAzureCredentialFactory, AzureCredentialFactory>();
        services.TryAddSingleton<TokenCredential>(provider => provider.GetRequiredService<IAzureCredentialFactory>().Create());
        return services;
    }

    /// <summary>
    /// Adds Entra ID sign-in (OpenID Connect) for the Blazor web app. Unauthenticated users are redirected to the
    /// Entra ID sign-in page because every endpoint requires an authenticated user unless it opts out explicitly.
    /// </summary>
    public static IServiceCollection AddProposalGeneratorWebAppAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = EntraIdConfigurationValidator.DefaultSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        var section = GetValidatedSection(configuration, sectionName);

        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(section);
        AddAuthenticatedFallbackPolicy(services);
        return services;
    }

    /// <summary>
    /// Adds Entra ID bearer-token validation for the API. Requests without a valid token receive 401 because every
    /// endpoint requires an authenticated user unless it opts out explicitly.
    /// </summary>
    public static IServiceCollection AddProposalGeneratorWebApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = EntraIdConfigurationValidator.DefaultSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        var section = GetValidatedSection(configuration, sectionName);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(section);
        AddAuthenticatedFallbackPolicy(services);
        return services;
    }

    private static IConfigurationSection GetValidatedSection(IConfiguration configuration, string sectionName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        var section = configuration.GetSection(sectionName);
        EntraIdConfigurationValidator.ThrowIfInvalid(section);
        return section;
    }

    private static void AddAuthenticatedFallbackPolicy(IServiceCollection services) =>
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
}
