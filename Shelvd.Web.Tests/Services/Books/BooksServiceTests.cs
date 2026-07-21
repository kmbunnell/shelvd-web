using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;
using Shelvd.Web.Services.Books;

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
}
