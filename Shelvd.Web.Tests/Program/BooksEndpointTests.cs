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
using Shelvd.Web.Services.Books;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests;

public class BooksEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BooksEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Supabase:Url", "https://test.supabase.co");
            builder.UseSetting("Supabase:AnonKey", "test-anon-key");
        });
    }

    private WebApplicationFactory<Program> WithBooksService(IBooksService booksService) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped(_ => booksService);
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultScheme = "Test";
            });
        }));

    [Fact]
    public async Task GetBooks_ReturnsOkWithBooks_WhenAuthenticatedAndServiceSucceeds()
    {
        var books = new List<BookDto>
        {
            new(Guid.NewGuid(), "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, [])
        };
        var booksService = new StubBooksService(new Result<IReadOnlyList<BookDto>, BooksError>.Success(books));
        var client = WithBooksService(booksService).CreateClient();

        var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<BookDto>>();
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal("Test Book", body[0].Title);
    }

    [Fact]
    public async Task GetBooks_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBooks_ReturnsNonSuccessStatus_WhenServiceFails()
    {
        var booksService = new StubBooksService(
            new Result<IReadOnlyList<BookDto>, BooksError>.Failure(BooksError.Unknown));
        var client = WithBooksService(booksService).CreateClient();

        var response = await client.GetAsync("/api/books");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBookById_ReturnsOkWithBook_WhenAuthenticatedAndServiceSucceeds()
    {
        var id = Guid.NewGuid();
        var book = new BookDto(id, "9780000000000", "Test Book", ["Author One"], null, DateTimeOffset.UtcNow, []);
        var booksService = new StubBooksService(byIdResult: new Result<BookDto?, BooksError>.Success(book));
        var client = WithBooksService(booksService).CreateClient();

        var response = await client.GetAsync($"/api/books/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BookDto>();
        Assert.NotNull(body);
        Assert.Equal("Test Book", body!.Title);
    }

    [Fact]
    public async Task GetBookById_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var id = Guid.NewGuid();
        var booksService = new StubBooksService(byIdResult: new Result<BookDto?, BooksError>.Success(null));
        var client = WithBooksService(booksService).CreateClient();

        var response = await client.GetAsync($"/api/books/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBookById_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var id = Guid.NewGuid();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/api/books/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBookById_ReturnsNonSuccessStatus_WhenServiceFails()
    {
        var id = Guid.NewGuid();
        var booksService = new StubBooksService(byIdResult: new Result<BookDto?, BooksError>.Failure(BooksError.Unknown));
        var client = WithBooksService(booksService).CreateClient();

        var response = await client.GetAsync($"/api/books/{id}");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class StubBooksService(
        Result<IReadOnlyList<BookDto>, BooksError>? result = null,
        Result<BookDto?, BooksError>? byIdResult = null) : IBooksService
    {
        public Task<Result<IReadOnlyList<BookDto>, BooksError>> GetBooksAsync(string accessToken) =>
            Task.FromResult(result!);

        public Task<Result<BookDto?, BooksError>> GetBookByIdAsync(string accessToken, Guid id) =>
            Task.FromResult(byIdResult!);
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
