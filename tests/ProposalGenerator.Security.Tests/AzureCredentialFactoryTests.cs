using Azure.Identity;
using Microsoft.Extensions.Options;

namespace ProposalGenerator.Security.Tests;

public sealed class AzureCredentialFactoryTests
{
    // Fictional identifiers; they do not belong to any real tenant or identity.
    private const string ConfiguredClientId = "11111111-2222-3333-4444-555555555555";
    private const string EnvironmentClientId = "66666666-7777-8888-9999-000000000000";
    private const string TenantId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    [Fact]
    public void Auto_without_managed_identity_endpoint_uses_developer_chain()
    {
        var factory = CreateFactory(new AzureCredentialOptions(), new FakeEnvironment());

        var plan = factory.Resolve();

        Assert.Equal(AzureCredentialKind.DeveloperCredentialChain, plan.Kind);
        Assert.Null(plan.ManagedIdentityClientId);
        Assert.IsType<DefaultAzureCredential>(factory.Create());
    }

    [Theory]
    [InlineData("IDENTITY_ENDPOINT")]
    [InlineData("MSI_ENDPOINT")]
    public void Auto_with_managed_identity_endpoint_uses_user_assigned_identity_from_environment(string endpointVariable)
    {
        var environment = new FakeEnvironment
        {
            [endpointVariable] = "http://localhost:42356/msi/token",
            [AzureCredentialFactory.ClientIdEnvironmentVariable] = EnvironmentClientId,
        };
        var factory = CreateFactory(new AzureCredentialOptions(), environment);

        var plan = factory.Resolve();

        Assert.Equal(AzureCredentialKind.UserAssignedManagedIdentity, plan.Kind);
        Assert.Equal(EnvironmentClientId, plan.ManagedIdentityClientId);
        Assert.IsType<ManagedIdentityCredential>(factory.Create());
    }

    [Fact]
    public void Auto_with_managed_identity_endpoint_and_no_client_id_uses_system_assigned_identity()
    {
        var environment = new FakeEnvironment { ["IDENTITY_ENDPOINT"] = "http://localhost:42356/msi/token" };
        var factory = CreateFactory(new AzureCredentialOptions(), environment);

        var plan = factory.Resolve();

        Assert.Equal(AzureCredentialKind.SystemAssignedManagedIdentity, plan.Kind);
        Assert.Null(plan.ManagedIdentityClientId);
        Assert.IsType<ManagedIdentityCredential>(factory.Create());
    }

    [Fact]
    public void Configured_client_id_takes_precedence_over_environment()
    {
        var environment = new FakeEnvironment
        {
            ["IDENTITY_ENDPOINT"] = "http://localhost:42356/msi/token",
            [AzureCredentialFactory.ClientIdEnvironmentVariable] = EnvironmentClientId,
        };
        var factory = CreateFactory(new AzureCredentialOptions { ManagedIdentityClientId = ConfiguredClientId }, environment);

        Assert.Equal(ConfiguredClientId, factory.Resolve().ManagedIdentityClientId);
    }

    [Fact]
    public void Blank_configured_client_id_falls_back_to_environment()
    {
        var environment = new FakeEnvironment
        {
            ["IDENTITY_ENDPOINT"] = "http://localhost:42356/msi/token",
            [AzureCredentialFactory.ClientIdEnvironmentVariable] = EnvironmentClientId,
        };
        var factory = CreateFactory(new AzureCredentialOptions { ManagedIdentityClientId = "  " }, environment);

        Assert.Equal(EnvironmentClientId, factory.Resolve().ManagedIdentityClientId);
    }

    [Fact]
    public void ManagedIdentity_mode_uses_managed_identity_without_endpoint_detection()
    {
        var options = new AzureCredentialOptions { Mode = AzureCredentialMode.ManagedIdentity, ManagedIdentityClientId = ConfiguredClientId };
        var factory = CreateFactory(options, new FakeEnvironment());

        var plan = factory.Resolve();

        Assert.Equal(AzureCredentialKind.UserAssignedManagedIdentity, plan.Kind);
        Assert.Equal(ConfiguredClientId, plan.ManagedIdentityClientId);
    }

    [Fact]
    public void Development_mode_ignores_managed_identity_endpoint()
    {
        var environment = new FakeEnvironment { ["IDENTITY_ENDPOINT"] = "http://localhost:42356/msi/token" };
        var options = new AzureCredentialOptions { Mode = AzureCredentialMode.Development, TenantId = TenantId };
        var factory = CreateFactory(options, environment);

        var plan = factory.Resolve();

        Assert.Equal(AzureCredentialKind.DeveloperCredentialChain, plan.Kind);
        Assert.Equal(TenantId, plan.TenantId);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("11111111-2222-3333-4444")]
    public void Invalid_client_id_is_rejected(string clientId)
    {
        var options = new AzureCredentialOptions { Mode = AzureCredentialMode.ManagedIdentity, ManagedIdentityClientId = clientId };
        var factory = CreateFactory(options, new FakeEnvironment());

        var error = Assert.Throws<InvalidOperationException>(factory.Resolve);
        Assert.Contains(nameof(AzureCredentialOptions.ManagedIdentityClientId), error.Message);
    }

    [Fact]
    public void Invalid_tenant_id_is_rejected()
    {
        var options = new AzureCredentialOptions { Mode = AzureCredentialMode.Development, TenantId = "contoso" };
        var factory = CreateFactory(options, new FakeEnvironment());

        var error = Assert.Throws<InvalidOperationException>(factory.Resolve);
        Assert.Contains(nameof(AzureCredentialOptions.TenantId), error.Message);
    }

    [Fact]
    public void Undefined_mode_is_rejected()
    {
        var factory = CreateFactory(new AzureCredentialOptions { Mode = (AzureCredentialMode)42 }, new FakeEnvironment());

        Assert.Throws<InvalidOperationException>(factory.Resolve);
    }

    [Fact]
    public void Developer_chain_excludes_managed_identity_and_interactive_sign_in()
    {
        var options = AzureCredentialFactory.CreateDeveloperOptions(TenantId);

        Assert.True(options.ExcludeManagedIdentityCredential);
        Assert.True(options.ExcludeWorkloadIdentityCredential);
        Assert.True(options.ExcludeInteractiveBrowserCredential);
        Assert.False(options.ExcludeAzureCliCredential);
        Assert.False(options.ExcludeAzureDeveloperCliCredential);
        Assert.Equal(TenantId, options.TenantId);
    }

    private static AzureCredentialFactory CreateFactory(AzureCredentialOptions options, FakeEnvironment environment) =>
        new(Options.Create(options), environment);
}
