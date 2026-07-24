using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shelvd.Web.Tests.Endpoints;

public class LogoutEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LogoutEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Supabase:Url", "https://test.supabase.co");
            builder.UseSetting("Supabase:AnonKey", "test-anon-key");
            builder.ConfigureServices(services =>
            {
                // Ephemeral keys so antiforgery tokens don't touch the real on-disk key store.
                services.AddDataProtection().UseEphemeralDataProtectionProvider();

                // Authenticate every request as a fixed test user, bypassing the real
                // cookie login flow so [Authorize] passes without a live Supabase session.
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultScheme = "Test";
                });
            });
        });
    }

    [Fact]
    public async Task PostLogout_ReturnsBadRequest_WhenNoAntiforgeryTokenProvided()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/logout", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostLogout_RedirectsHome_WhenValidAntiforgeryTokenProvided()
    {
        // HandleCookies must be off: WebApplicationFactory's default cookie handler overwrites
        // a manually-set Cookie header with its own (empty) container before the request is sent.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        using var scope = _factory.Services.CreateScope();
        var antiforgery = scope.ServiceProvider.GetRequiredService<IAntiforgery>();
        // Antiforgery tokens are bound to the current user's claims — mint them against the
        // same principal TestAuthHandler will attach to the real request, or validation fails
        // with "token was meant for a different claims-based user".
        var tokenContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = TestAuthHandler.CreatePrincipal(),
        };
        var tokens = antiforgery.GetAndStoreTokens(tokenContext);
        // Response.Headers.SetCookie carries attributes like "path=/; samesite=strict" after
        // the value — a request Cookie header must only be "name=value", so strip those off.
        var setCookieValue = tokenContext.Response.Headers.SetCookie.ToString().Split(';')[0];

        var request = new HttpRequestMessage(HttpMethod.Post, "/logout")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [tokens.FormFieldName] = tokens.RequestToken!,
            }),
        };
        request.Headers.Add("Cookie", setCookieValue);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        public static ClaimsPrincipal CreatePrincipal()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "test-user"), new Claim(ClaimTypes.Name, "test@example.com")],
                "Test");
            return new ClaimsPrincipal(identity);
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var ticket = new AuthenticationTicket(CreatePrincipal(), "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
