using Microsoft.Extensions.Configuration;

namespace ProposalGenerator.Security;

/// <summary>
/// Validates the Microsoft.Identity.Web configuration section before it is bound, so the app fails at start-up
/// instead of running with incomplete sign-in settings or with an app registration secret in configuration.
/// </summary>
public static class EntraIdConfigurationValidator
{
    /// <summary>Default Microsoft.Identity.Web configuration section.</summary>
    public const string DefaultSectionName = "AzureAd";

    internal static readonly string[] RequiredSettings = ["Instance", "TenantId", "ClientId"];

    // The only credential source that needs no secret: a federated credential issued to a managed identity.
    internal const string KeylessCredentialSourceType = "SignedAssertionFromManagedIdentity";

    private static readonly string[] SecretSettings = ["ClientSecret"];
    private static readonly string[] CredentialCollections = ["ClientCredentials", "ClientCertificates"];

    /// <summary>
    /// Returns every configuration problem found in <paramref name="section"/>; an empty list means the section is valid.
    /// </summary>
    public static IReadOnlyList<string> Validate(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var errors = new List<string>();

        foreach (var setting in RequiredSettings)
        {
            if (string.IsNullOrWhiteSpace(section[setting]))
            {
                errors.Add($"{section.Path}:{setting} is required.");
            }
        }

        if (!string.IsNullOrWhiteSpace(section["Instance"])
            && (!Uri.TryCreate(section["Instance"], UriKind.Absolute, out var instance) || instance.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add($"{section.Path}:Instance must be an absolute https URL.");
        }

        if (!string.IsNullOrWhiteSpace(section["ClientId"]) && !Guid.TryParse(section["ClientId"], out _))
        {
            errors.Add($"{section.Path}:ClientId must be a GUID.");
        }

        foreach (var setting in SecretSettings)
        {
            if (!string.IsNullOrEmpty(section[setting]))
            {
                errors.Add($"{section.Path}:{setting} is not allowed. Use a managed identity federated credential instead of an app registration secret.");
            }
        }

        foreach (var collection in CredentialCollections)
        {
            foreach (var credential in section.GetSection(collection).GetChildren())
            {
                var sourceType = credential["SourceType"];
                if (!string.Equals(sourceType, KeylessCredentialSourceType, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{credential.Path}:SourceType '{sourceType}' is not allowed. Only {KeylessCredentialSourceType} is keyless.");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> listing every problem when <paramref name="section"/> is invalid.
    /// </summary>
    public static void ThrowIfInvalid(IConfigurationSection section)
    {
        var errors = Validate(section);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Entra ID sign-in configuration is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
    }
}
