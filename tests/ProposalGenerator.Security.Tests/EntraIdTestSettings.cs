namespace ProposalGenerator.Security.Tests;

internal static class EntraIdTestSettings
{
    // Fictional identifiers; they do not belong to any real tenant or app registration.
    public const string TenantId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    public const string ClientId = "11111111-2222-3333-4444-555555555555";

    public static Dictionary<string, string?> Valid() => new()
    {
        ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
        ["AzureAd:TenantId"] = TenantId,
        ["AzureAd:ClientId"] = ClientId,
        ["AzureAd:Audience"] = $"api://{ClientId}",
        ["AzureAd:CallbackPath"] = "/signin-oidc",
    };
}
