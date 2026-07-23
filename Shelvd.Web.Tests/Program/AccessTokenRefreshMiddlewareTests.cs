using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Shelvd.Web.Services.Auth;

namespace Shelvd.Web.Tests;

public class AccessTokenRefreshMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccessTokenRefreshMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static string CreateAccessToken(DateTime expiresUtc) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(expires: expiresUtc));

    private WebApplicationFactory<Program> CreateFactory(HttpStatusCode statusCode, object? content)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Supabase:Url", "https://test.supabase.co");
            builder.UseSetting("Supabase:AnonKey", "test-anon-key");
            builder.ConfigureServices(services =>
            {
                var handler = new Mock<HttpMessageHandler>();
                handler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage(statusCode)
                    {
                        Content = content is null ? null : JsonContent.Create(content)
                    });

                // Overrides the primary handler for the named "Gotrue" HttpClient registered in
                // Program.cs, so ServerAuthService.RefreshSessionAsync hits this fake instead of
                // a real Supabase endpoint.
                services
                    .AddHttpClient(ServerAuthService.GotrueHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => handler.Object);
            });
        });
    }

    private static async Task<string> MintCookieAsync(WebApplicationFactory<Program> factory, string accessToken, string refreshToken)
    {
        using var scope = factory.Services.CreateScope();
        var cookieContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(cookieContext);
        var cookieService = new HttpContextAuthCookieService(accessor.Object);
        var session = new AuthSession("user-1", "user@example.com", accessToken, refreshToken);
        var properties = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        };

        await cookieService.SignInAsync(session, properties);

        return cookieContext.Response.Headers.SetCookie.ToString().Split(';')[0];
    }

    private static string AuthCookieName(string cookieHeader) => cookieHeader.Split('=')[0];

    private static bool ContainsCookieNamed(HttpResponseMessage response, string cookieName) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
        && values.Any(value => value.StartsWith($"{cookieName}=", StringComparison.Ordinal));

    [Fact]
    public async Task GetProtectedRoute_ReturnsRefreshedCookie_WhenAccessTokenIsNearExpiry()
    {
        var factory = CreateFactory(HttpStatusCode.OK, new
        {
            access_token = CreateAccessToken(DateTime.UtcNow.AddMinutes(30)),
            refresh_token = "new-refresh-token",
            user = new { id = "user-1", email = "user@example.com" }
        });
        var staleAccessToken = CreateAccessToken(DateTime.UtcNow.AddSeconds(10));
        var cookie = await MintCookieAsync(factory, staleAccessToken, "refresh-token");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/library");
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(ContainsCookieNamed(response, AuthCookieName(cookie)));
    }

    [Fact]
    public async Task GetProtectedRoute_RedirectsToLogin_WhenRefreshFails()
    {
        var factory = CreateFactory(HttpStatusCode.Unauthorized, content: null);
        var staleAccessToken = CreateAccessToken(DateTime.UtcNow.AddSeconds(10));
        var cookie = await MintCookieAsync(factory, staleAccessToken, "refresh-token");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/library");
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther,
            $"Expected a redirect, got {response.StatusCode}.");
        Assert.Equal("/login", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task GetProtectedRoute_DoesNotChangeCookie_WhenAccessTokenIsFresh()
    {
        var factory = CreateFactory(HttpStatusCode.OK, new
        {
            access_token = CreateAccessToken(DateTime.UtcNow.AddMinutes(30)),
            refresh_token = "new-refresh-token",
            user = new { id = "user-1", email = "user@example.com" }
        });
        var freshAccessToken = CreateAccessToken(DateTime.UtcNow.AddMinutes(30));
        var cookie = await MintCookieAsync(factory, freshAccessToken, "refresh-token");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/library");
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(ContainsCookieNamed(response, AuthCookieName(cookie)));
    }
}
