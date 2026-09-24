using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProposalGenerator.Api.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_ReturnsOkWithHealthyStatus()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
