using System.Net;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ProposalGenerator.Security.Tests;

public sealed class EntraIdAuthenticationTests
{
    // Fictional endpoint: tests never contact Entra ID; the OpenID configuration is supplied in-process.
    private const string AuthorizationEndpoint = "https://login.example.test/fictional-tenant/oauth2/v2.0/authorize";

    [Fact]
    public async Task Web_app_redirects_anonymous_user_to_entra_id_sign_in()
    {
        await using var app = await StartAsync(services =>
        {
            services.AddProposalGeneratorWebAppAuthentication(Configuration(EntraIdTestSettings.Valid()));
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                    new OpenIdConnectConfiguration { AuthorizationEndpoint = AuthorizationEndpoint }));
        });

        using var response = await app.GetTestClient().GetAsync(new Uri("/proposals", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.StartsWith(AuthorizationEndpoint + "?", location.AbsoluteUri, StringComparison.Ordinal);
        var query = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal(EntraIdTestSettings.ClientId, query["client_id"]);
        // Sign-in only: the ID token arrives on the front channel, so no client secret is needed to redeem a code.
        Assert.Equal("id_token", query["response_type"]);
    }

    [Fact]
    public async Task Web_api_rejects_request_without_token()
    {
        await using var app = await StartAsync(services =>
            services.AddProposalGeneratorWebApiAuthentication(Configuration(EntraIdTestSettings.Valid())));

        using var response = await app.GetTestClient().GetAsync(new Uri("/proposals", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task Web_api_allows_endpoint_that_opts_out_explicitly()
    {
        await using var app = await StartAsync(services =>
            services.AddProposalGeneratorWebApiAuthentication(Configuration(EntraIdTestSettings.Valid())));

        using var response = await app.GetTestClient().GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Registration_fails_fast_when_configuration_contains_a_client_secret()
    {
        var settings = EntraIdTestSettings.Valid();
        settings["AzureAd:ClientSecret"] = "placeholder-not-a-real-secret";

        var error = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddProposalGeneratorWebAppAuthentication(Configuration(settings)));
        Assert.Contains("ClientSecret is not allowed", error.Message);
    }

    [Fact]
    public void Registration_fails_fast_when_section_is_missing()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddProposalGeneratorWebApiAuthentication(Configuration(new Dictionary<string, string?>())));
        Assert.Contains("AzureAd:ClientId is required.", error.Message);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static async Task<WebApplication> StartAsync(Action<IServiceCollection> configureServices)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        configureServices(builder.Services);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/proposals", () => Results.Ok("protected"));
        app.MapGet("/health", () => Results.Ok("healthy")).AllowAnonymous();
        await app.StartAsync();
        return app;
    }
}
