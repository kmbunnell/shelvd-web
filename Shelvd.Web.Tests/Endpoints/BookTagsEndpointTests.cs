using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shelvd.Web.Services.Auth;
using Shelvd.Web.Services.BookTags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Endpoints;

public class BookTagsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Guid _bookId = Guid.NewGuid();
    private readonly Guid _tagId = Guid.NewGuid();

    public BookTagsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Supabase:Url", "https://test.supabase.co");
            builder.UseSetting("Supabase:AnonKey", "test-anon-key");
        });
    }

    private WebApplicationFactory<Program> WithBookTagsService(IBookTagsService bookTagsService) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped(_ => bookTagsService);
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultScheme = "Test";
            });
        }));

    [Fact]
    public async Task TagBook_ReturnsNoContent_WhenAuthenticatedAndServiceSucceeds()
    {
        var bookTagsService = new StubBookTagsService(new Result<BookTagsError>.Success());
        var client = WithBookTagsService(bookTagsService).CreateClient();

        var response = await client.PutAsync($"/api/books/{_bookId}/tags/{_tagId}", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TagBook_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PutAsync($"/api/books/{_bookId}/tags/{_tagId}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TagBook_ReturnsUnauthorized_WhenServiceReturnsUnauthenticated()
    {
        var bookTagsService = new StubBookTagsService(
            new Result<BookTagsError>.Failure(BookTagsError.Unauthenticated));
        var client = WithBookTagsService(bookTagsService).CreateClient();

        var response = await client.PutAsync($"/api/books/{_bookId}/tags/{_tagId}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TagBook_ReturnsBadGateway_WhenServiceFailsForOtherReasons()
    {
        var bookTagsService = new StubBookTagsService(
            new Result<BookTagsError>.Failure(BookTagsError.Unknown));
        var client = WithBookTagsService(bookTagsService).CreateClient();

        var response = await client.PutAsync($"/api/books/{_bookId}/tags/{_tagId}", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task UntagBook_ReturnsNoContent_WhenAuthenticatedAndServiceSucceeds()
    {
        var bookTagsService = new StubBookTagsService(new Result<BookTagsError>.Success());
        var client = WithBookTagsService(bookTagsService).CreateClient();

        var response = await client.DeleteAsync($"/api/books/{_bookId}/tags/{_tagId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UntagBook_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.DeleteAsync($"/api/books/{_bookId}/tags/{_tagId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UntagBook_ReturnsBadGateway_WhenServiceFailsForOtherReasons()
    {
        var bookTagsService = new StubBookTagsService(
            new Result<BookTagsError>.Failure(BookTagsError.Unknown));
        var client = WithBookTagsService(bookTagsService).CreateClient();

        var response = await client.DeleteAsync($"/api/books/{_bookId}/tags/{_tagId}");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private sealed class StubBookTagsService(Result<BookTagsError> result) : IBookTagsService
    {
        public Task<Result<BookTagsError>> TagBookAsync(string accessToken, Guid bookId, Guid tagId) =>
            Task.FromResult(result);

        public Task<Result<BookTagsError>> UntagBookAsync(string accessToken, Guid bookId, Guid tagId) =>
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
