using Microsoft.Extensions.Configuration;

namespace ProposalGenerator.Security.Tests;

public sealed class EntraIdConfigurationValidatorTests
{
    [Fact]
    public void Accepts_complete_secretless_configuration()
    {
        var section = Section(EntraIdTestSettings.Valid());

        Assert.Empty(EntraIdConfigurationValidator.Validate(section));
    }

    [Theory]
    [InlineData("Instance")]
    [InlineData("TenantId")]
    [InlineData("ClientId")]
    public void Requires_setting(string setting)
    {
        var settings = EntraIdTestSettings.Valid();
        settings.Remove($"AzureAd:{setting}");

        var errors = EntraIdConfigurationValidator.Validate(Section(settings));

        Assert.Contains($"AzureAd:{setting} is required.", errors);
    }

    [Fact]
    public void Rejects_client_secret()
    {
        var settings = EntraIdTestSettings.Valid();
        settings["AzureAd:ClientSecret"] = "placeholder-not-a-real-secret";

        var errors = EntraIdConfigurationValidator.Validate(Section(settings));

        Assert.Single(errors);
        Assert.Contains("ClientSecret is not allowed", errors[0]);
    }

    [Theory]
    [InlineData("ClientCredentials", "ClientSecret")]
    [InlineData("ClientCredentials", "Base64Encoded")]
    [InlineData("ClientCertificates", "Path")]
    [InlineData("ClientCertificates", "KeyVault")]
    public void Rejects_credential_sources_other_than_managed_identity(string collection, string sourceType)
    {
        var settings = EntraIdTestSettings.Valid();
        settings[$"AzureAd:{collection}:0:SourceType"] = sourceType;

        var errors = EntraIdConfigurationValidator.Validate(Section(settings));

        Assert.Single(errors);
        Assert.Contains($"'{sourceType}' is not allowed", errors[0]);
    }

    [Fact]
    public void Accepts_managed_identity_federated_credential()
    {
        var settings = EntraIdTestSettings.Valid();
        settings["AzureAd:ClientCredentials:0:SourceType"] = "SignedAssertionFromManagedIdentity";
        settings["AzureAd:ClientCredentials:0:ManagedIdentityClientId"] = "66666666-7777-8888-9999-000000000000";

        Assert.Empty(EntraIdConfigurationValidator.Validate(Section(settings)));
    }

    [Theory]
    [InlineData("Instance", "http://login.microsoftonline.com/", "absolute https URL")]
    [InlineData("Instance", "login.microsoftonline.com", "absolute https URL")]
    [InlineData("ClientId", "proposal-generator", "must be a GUID")]
    public void Rejects_malformed_values(string setting, string value, string expected)
    {
        var settings = EntraIdTestSettings.Valid();
        settings[$"AzureAd:{setting}"] = value;

        var errors = EntraIdConfigurationValidator.Validate(Section(settings));

        Assert.Single(errors);
        Assert.Contains(expected, errors[0]);
    }

    [Fact]
    public void ThrowIfInvalid_lists_every_problem()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => EntraIdConfigurationValidator.ThrowIfInvalid(Section(new Dictionary<string, string?>())));

        Assert.Contains("AzureAd:Instance", error.Message);
        Assert.Contains("AzureAd:TenantId", error.Message);
        Assert.Contains("AzureAd:ClientId", error.Message);
    }

    private static IConfigurationSection Section(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection("AzureAd");
}
