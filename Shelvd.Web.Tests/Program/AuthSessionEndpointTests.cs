using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Shelvd.Web.Tests;

public class AuthSessionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthSessionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Supabase:Url", "https://test.supabase.co")
            .UseSetting("Supabase:AnonKey", "test-anon-key"));
    }

    [Fact]
    public async Task GetAuthSession_ReturnsNotFound_WhenRouteRemoved()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
