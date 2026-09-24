using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ProposalGenerator.Security.Tests;

public sealed class AzureCredentialRegistrationTests
{
    [Fact]
    public void Binds_options_from_configuration_and_registers_credential()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Azure:Credential:Mode"] = "ManagedIdentity",
            ["Azure:Credential:ManagedIdentityClientId"] = "11111111-2222-3333-4444-555555555555",
        });

        var plan = provider.GetRequiredService<IAzureCredentialFactory>().Resolve();

        Assert.Equal(AzureCredentialKind.UserAssignedManagedIdentity, plan.Kind);
        Assert.Equal("11111111-2222-3333-4444-555555555555", plan.ManagedIdentityClientId);
        Assert.IsType<ManagedIdentityCredential>(provider.GetRequiredService<TokenCredential>());
        Assert.Same(provider.GetRequiredService<TokenCredential>(), provider.GetRequiredService<TokenCredential>());
    }

    [Theory]
    [InlineData("Azure:Credential:ManagedIdentityClientId", "not-a-guid")]
    [InlineData("Azure:Credential:TenantId", "contoso.onmicrosoft.example")]
    public void Rejects_invalid_identifiers_when_options_are_read(string key, string value)
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { [key] = value });

        var error = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AzureCredentialOptions>>().Value);
        Assert.Contains(key.Split(':')[^1], error.Message);
    }

    [Fact]
    public void Rejects_unknown_mode_when_options_are_read()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Azure:Credential:Mode"] = "42" });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AzureCredentialOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IEnvironmentVariableReader>(new FakeEnvironment());
        services.AddProposalGeneratorAzureCredential(configuration);
        return services.BuildServiceProvider();
    }
}
