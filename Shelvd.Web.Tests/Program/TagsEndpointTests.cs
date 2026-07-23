using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Services.Auth;
using Shelvd.Web.Services.Tags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests;

public class TagsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TagsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Supabase:Url", "https://test.supabase.co");
            builder.UseSetting("Supabase:AnonKey", "test-anon-key");
        });
    }

    private WebApplicationFactory<Program> WithTagsService(ITagsService tagsService) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped(_ => tagsService);
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultScheme = "Test";
            });
        }));

    [Fact]
    public async Task GetTags_ReturnsOkWithTags_WhenAuthenticatedAndServiceSucceeds()
    {
        var tags = new List<TagDto>
        {
            new(Guid.NewGuid(), "Fantasy", false)
        };
        var tagsService = new StubTagsService(new Result<IReadOnlyList<TagDto>, TagsError>.Success(tags));
        var client = WithTagsService(tagsService).CreateClient();

        var response = await client.GetAsync("/api/tags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<TagDto>>();
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal("Fantasy", body[0].Name);
    }

    [Fact]
    public async Task GetTags_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/tags");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTags_ReturnsUnauthorized_WhenServiceReturnsUnauthenticated()
    {
        var tagsService = new StubTagsService(
            new Result<IReadOnlyList<TagDto>, TagsError>.Failure(TagsError.Unauthenticated));
        var client = WithTagsService(tagsService).CreateClient();

        var response = await client.GetAsync("/api/tags");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTags_ReturnsBadGateway_WhenServiceFailsForOtherReasons()
    {
        var tagsService = new StubTagsService(
            new Result<IReadOnlyList<TagDto>, TagsError>.Failure(TagsError.Unknown));
        var client = WithTagsService(tagsService).CreateClient();

        var response = await client.GetAsync("/api/tags");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private sealed class StubTagsService(Result<IReadOnlyList<TagDto>, TagsError> result) : ITagsService
    {
        public Task<Result<IReadOnlyList<TagDto>, TagsError>> GetTagsAsync(string accessToken) =>
            Task.FromResult(result);
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "test-user"),
                    new Claim(AuthClaimTypes.AccessToken, "test-access-token")
                ],
                "Test");
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
