using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Services.Books;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Services.Books;

public class BooksServiceTests
{
    private static readonly NullLogger<BooksService> _logger = NullLogger<BooksService>.Instance;

    private static Mock<IHttpClientFactory> CreateHttpClientFactory(
        HttpStatusCode statusCode,
        object? content,
        Action<HttpRequestMessage>? onRequest = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => onRequest?.Invoke(request))
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = content is null ? null : JsonContent.Create(content)
            });

        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(BooksService.SupabaseRestHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/rest/v1/") });
        return factory;
    }

    private static Mock<IHttpClientFactory> CreateThrowingHttpClientFactory(Exception exception)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(BooksService.SupabaseRestHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/rest/v1/") });
        return factory;
    }

    [Fact]
    public async Task GetBooksAsync_RequestsBooksWithBearerToken()
    {
        HttpRequestMessage? capturedRequest = null;
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, Array.Empty<object>(), request => capturedRequest = request);
        var sut = new BooksService(factory.Object, _logger);

        await sut.GetBooksAsync("access-token");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("/rest/v1/books", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Contains("select=", capturedRequest.RequestUri.Query);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("access-token", capturedRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetBooksAsync_ReturnsParsedBooks_WhenResponseIsSuccessful()
    {
        var responseBooks = new[]
        {
            new
            {
                id = "11111111-1111-1111-1111-111111111111",
                isbn = "9780000000000",
                title = "Test Book",
                authors = new[] { "Author One", "Author Two" },
                cover_image_url = "https://example.com/cover.jpg",
                created_at = "2024-01-01T00:00:00Z"
            }
        };
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, responseBooks);
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBooksAsync("access-token");

        var success = Assert.IsType<Result<IReadOnlyList<BookDto>, BooksError>.Success>(result);
        var book = Assert.Single(success.Value);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), book.Id);
        Assert.Equal("Test Book", book.Title);
        Assert.Equal(new[] { "Author One", "Author Two" }, book.Authors);
        Assert.Equal("9780000000000", book.Isbn);
        Assert.Equal("https://example.com/cover.jpg", book.CoverImageUrl);
    }

    [Fact]
    public async Task GetBooksAsync_RequestsBookTagsEmbed()
    {
        HttpRequestMessage? capturedRequest = null;
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, Array.Empty<object>(), request => capturedRequest = request);
        var sut = new BooksService(factory.Object, _logger);

        await sut.GetBooksAsync("access-token");

        Assert.NotNull(capturedRequest);
        Assert.Contains("book_tags", Uri.UnescapeDataString(capturedRequest!.RequestUri!.Query));
    }

    [Fact]
    public async Task GetBooksAsync_MapsEmbeddedBookTags_ToBookDtoTags()
    {
        var responseBooks = new[]
        {
            new
            {
                id = "11111111-1111-1111-1111-111111111111",
                isbn = "9780000000000",
                title = "Test Book",
                authors = new[] { "Author One" },
                cover_image_url = (string?)null,
                created_at = "2024-01-01T00:00:00Z",
                book_tags = new[]
                {
                    new { tags = new { id = "22222222-2222-2222-2222-222222222222", name = "Fantasy", is_default = false } },
                    new { tags = new { id = "33333333-3333-3333-3333-333333333333", name = "Favorites", is_default = true } }
                }
            }
        };
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, responseBooks);
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBooksAsync("access-token");

        var success = Assert.IsType<Result<IReadOnlyList<BookDto>, BooksError>.Success>(result);
        var book = Assert.Single(success.Value);
        Assert.Equal(2, book.Tags.Count);
        Assert.Contains(book.Tags, t => t.Name == "Fantasy" && !t.IsDefault);
        Assert.Contains(book.Tags, t => t.Name == "Favorites" && t.IsDefault);
    }

    [Fact]
    public async Task GetBooksAsync_MapsBookWithNoTags_ToEmptyTagsList()
    {
        var responseBooks = new[]
        {
            new
            {
                id = "11111111-1111-1111-1111-111111111111",
                isbn = "9780000000000",
                title = "Test Book",
                authors = new[] { "Author One" },
                cover_image_url = (string?)null,
                created_at = "2024-01-01T00:00:00Z",
                book_tags = Array.Empty<object>()
            }
        };
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, responseBooks);
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBooksAsync("access-token");

        var success = Assert.IsType<Result<IReadOnlyList<BookDto>, BooksError>.Success>(result);
        var book = Assert.Single(success.Value);
        Assert.NotNull(book.Tags);
        Assert.Empty(book.Tags);
    }

    [Fact]
    public async Task GetBooksAsync_ReturnsEmptyList_WhenResponseIsEmptyArray()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, Array.Empty<object>());
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBooksAsync("access-token");

        var success = Assert.IsType<Result<IReadOnlyList<BookDto>, BooksError>.Success>(result);
        Assert.Empty(success.Value);
    }

    [Fact]
    public async Task GetBooksAsync_ReturnsFailure_WhenResponseIsNotSuccessStatusCode()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.Unauthorized, content: null);
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBooksAsync("access-token");

        Assert.IsType<Result<IReadOnlyList<BookDto>, BooksError>.Failure>(result);
    }

    [Fact]
    public async Task GetBooksAsync_ReturnsFailure_WhenHttpCallThrows()
    {
        var factory = CreateThrowingHttpClientFactory(new HttpRequestException("network unreachable"));
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBooksAsync("access-token");

        var failure = Assert.IsType<Result<IReadOnlyList<BookDto>, BooksError>.Failure>(result);
        Assert.Equal(BooksError.NetworkError, failure.Error);
    }

    [Fact]
    public async Task GetBookByIdAsync_RequestsBookByIdFilterWithBearerToken()
    {
        var id = Guid.NewGuid();
        HttpRequestMessage? capturedRequest = null;
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, Array.Empty<object>(), request => capturedRequest = request);
        var sut = new BooksService(factory.Object, _logger);

        await sut.GetBookByIdAsync("access-token", id);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("/rest/v1/books", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Contains($"id=eq.{id}", Uri.UnescapeDataString(capturedRequest.RequestUri.Query));
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("access-token", capturedRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetBookByIdAsync_ReturnsMappedBook_WhenResponseIsSuccessful()
    {
        var id = Guid.NewGuid();
        var responseBooks = new[]
        {
            new
            {
                id = id.ToString(),
                isbn = "9780000000000",
                title = "Test Book",
                authors = new[] { "Author One" },
                cover_image_url = "https://example.com/cover.jpg",
                created_at = "2024-01-01T00:00:00Z",
                book_tags = new[]
                {
                    new { tags = new { id = "22222222-2222-2222-2222-222222222222", name = "Fantasy", is_default = false } }
                }
            }
        };
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, responseBooks);
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBookByIdAsync("access-token", id);

        var success = Assert.IsType<Result<BookDto?, BooksError>.Success>(result);
        Assert.NotNull(success.Value);
        Assert.Equal(id, success.Value!.Id);
        Assert.Equal("Test Book", success.Value.Title);
        Assert.Single(success.Value.Tags);
    }

    [Fact]
    public async Task GetBookByIdAsync_ReturnsSuccessWithNull_WhenResponseIsEmptyArray()
    {
        var id = Guid.NewGuid();
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, Array.Empty<object>());
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBookByIdAsync("access-token", id);

        var success = Assert.IsType<Result<BookDto?, BooksError>.Success>(result);
        Assert.Null(success.Value);
    }

    [Fact]
    public async Task GetBookByIdAsync_ReturnsFailure_WhenUnauthorized()
    {
        var id = Guid.NewGuid();
        var factory = CreateHttpClientFactory(HttpStatusCode.Unauthorized, content: null);
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBookByIdAsync("access-token", id);

        var failure = Assert.IsType<Result<BookDto?, BooksError>.Failure>(result);
        Assert.Equal(BooksError.Unauthenticated, failure.Error);
    }

    [Fact]
    public async Task GetBookByIdAsync_ReturnsFailure_WhenHttpCallThrows()
    {
        var id = Guid.NewGuid();
        var factory = CreateThrowingHttpClientFactory(new HttpRequestException("network unreachable"));
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBookByIdAsync("access-token", id);

        var failure = Assert.IsType<Result<BookDto?, BooksError>.Failure>(result);
        Assert.Equal(BooksError.NetworkError, failure.Error);
    }

    [Fact]
    public async Task GetBookByIdAsync_ReturnsFailure_WhenResponseIsMalformed()
    {
        var id = Guid.NewGuid();
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<object?>(null)
            });
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(BooksService.SupabaseRestHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/rest/v1/") });
        var sut = new BooksService(factory.Object, _logger);

        var result = await sut.GetBookByIdAsync("access-token", id);

        var failure = Assert.IsType<Result<BookDto?, BooksError>.Failure>(result);
        Assert.Equal(BooksError.MalformedResponse, failure.Error);
    }
}
