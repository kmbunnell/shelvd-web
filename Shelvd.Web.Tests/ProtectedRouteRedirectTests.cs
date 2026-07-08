using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Shelvd.Web.Tests;

public class ProtectedRouteRedirectTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProtectedRouteRedirectTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Supabase:Url", "https://test.supabase.co")
            .UseSetting("Supabase:AnonKey", "test-anon-key"));
    }

    [Fact]
    public async Task GetProtectedRoute_RedirectsToLoginWithReturnUrl_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/counter");

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther,
            $"Expected a redirect, got {response.StatusCode}.");
        var location = response.Headers.Location!;
        Assert.Equal("/login", location.AbsolutePath);
        Assert.Contains("returnUrl=", location.Query);
        Assert.Contains(Uri.EscapeDataString("/counter"), location.Query);
    }
}
