using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shelvd.Web.Client.Services.Books;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Services.Books;

public class BooksApiClientTests
{
    private static readonly NullLogger<BooksApiClient> _logger = NullLogger<BooksApiClient>.Instance;

    private static HttpClient CreateHttpClient(
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

        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://app.example.com/") };
    }

    private static HttpClient CreateThrowingHttpClient(Exception exception)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://app.example.com/") };
    }

    [Fact]
    public async Task GetBooksAsync_RequestsApiBooks()
    {
        HttpRequestMessage? capturedRequest = null;
        var httpClient = CreateHttpClient(HttpStatusCode.OK, Array.Empty<object>(), request => capturedRequest = request);
        var sut = new BooksApiClient(httpClient, _logger);

        await sut.GetBooksAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("/api/books", capturedRequest.RequestUri!.AbsolutePath);
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
                authors = new[] { "Author One" },
                cover_image_url = (string?)null,
                created_at = "2024-01-01T00:00:00Z"
            }
        };
        var httpClient = CreateHttpClient(HttpStatusCode.OK, responseBooks);
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.GetBooksAsync();

        var success = Assert.IsType<Result<IReadOnlyList<Shelvd.Web.Client.Models.BookDto>, BooksApiError>.Success>(result);
        var book = Assert.Single(success.Value);
        Assert.Equal("Test Book", book.Title);
    }

    [Fact]
    public async Task GetBooksAsync_ReturnsFailure_WhenResponseIsNotSuccessStatusCode()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.Unauthorized, content: null);
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.GetBooksAsync();

        Assert.IsType<Result<IReadOnlyList<Shelvd.Web.Client.Models.BookDto>, BooksApiError>.Failure>(result);
    }

    [Fact]
    public async Task GetBooksAsync_ReturnsFailure_WhenHttpCallThrows()
    {
        var httpClient = CreateThrowingHttpClient(new HttpRequestException("network unreachable"));
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.GetBooksAsync();

        var failure = Assert.IsType<Result<IReadOnlyList<Shelvd.Web.Client.Models.BookDto>, BooksApiError>.Failure>(result);
        Assert.Equal(BooksApiError.NetworkError, failure.Error);
    }

    [Fact]
    public async Task GetBooksAsync_ThrowsOperationCanceled_WhenTokenIsAlreadyCancelled()
    {
        var httpClient = CreateThrowingHttpClient(new OperationCanceledException());
        var sut = new BooksApiClient(httpClient, _logger);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.GetBooksAsync(cts.Token));
    }

    [Fact]
    public async Task GetBookAsync_RequestsApiBooksById()
    {
        var id = Guid.NewGuid();
        HttpRequestMessage? capturedRequest = null;
        var httpClient = CreateHttpClient(HttpStatusCode.OK, new
        {
            id = id.ToString(),
            isbn = "9780000000000",
            title = "Test Book",
            authors = new[] { "Author One" },
            cover_image_url = (string?)null,
            created_at = "2024-01-01T00:00:00Z",
            tags = Array.Empty<object>()
        }, request => capturedRequest = request);
        var sut = new BooksApiClient(httpClient, _logger);

        await sut.GetBookAsync(id);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal($"/api/books/{id}", capturedRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetBookAsync_ReturnsParsedBook_WhenResponseIsSuccessful()
    {
        var id = Guid.NewGuid();
        var responseBook = new
        {
            id = id.ToString(),
            isbn = "9780000000000",
            title = "Test Book",
            authors = new[] { "Author One" },
            cover_image_url = (string?)null,
            created_at = "2024-01-01T00:00:00Z",
            tags = Array.Empty<object>()
        };
        var httpClient = CreateHttpClient(HttpStatusCode.OK, responseBook);
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.GetBookAsync(id);

        var success = Assert.IsType<Result<Shelvd.Web.Client.Models.BookDto, BooksApiError>.Success>(result);
        Assert.Equal("Test Book", success.Value.Title);
    }

    [Fact]
    public async Task GetBookAsync_ReturnsFailureNotFound_WhenResponseIs404()
    {
        var id = Guid.NewGuid();
        var httpClient = CreateHttpClient(HttpStatusCode.NotFound, content: null);
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.GetBookAsync(id);

        var failure = Assert.IsType<Result<Shelvd.Web.Client.Models.BookDto, BooksApiError>.Failure>(result);
        Assert.Equal(BooksApiError.NotFound, failure.Error);
    }

    [Fact]
    public async Task GetBookAsync_ReturnsFailureUnauthenticated_WhenResponseIs401()
    {
        var id = Guid.NewGuid();
        var httpClient = CreateHttpClient(HttpStatusCode.Unauthorized, content: null);
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.GetBookAsync(id);

        var failure = Assert.IsType<Result<Shelvd.Web.Client.Models.BookDto, BooksApiError>.Failure>(result);
        Assert.Equal(BooksApiError.Unauthenticated, failure.Error);
    }

    [Fact]
    public async Task GetBookAsync_ReturnsFailureNetworkError_WhenHttpCallThrows()
    {
        var id = Guid.NewGuid();
        var httpClient = CreateThrowingHttpClient(new HttpRequestException("network unreachable"));
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.GetBookAsync(id);

        var failure = Assert.IsType<Result<Shelvd.Web.Client.Models.BookDto, BooksApiError>.Failure>(result);
        Assert.Equal(BooksApiError.NetworkError, failure.Error);
    }

    [Fact]
    public async Task DeleteBookAsync_SendsDeleteToBooksRoute()
    {
        var id = Guid.NewGuid();
        HttpRequestMessage? capturedRequest = null;
        var httpClient = CreateHttpClient(HttpStatusCode.NoContent, content: null, request => capturedRequest = request);
        var sut = new BooksApiClient(httpClient, _logger);

        await sut.DeleteBookAsync(id);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);
        Assert.Equal($"/api/books/{id}", capturedRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DeleteBookAsync_ReturnsSuccess_WhenResponseIsSuccessful()
    {
        var id = Guid.NewGuid();
        var httpClient = CreateHttpClient(HttpStatusCode.NoContent, content: null);
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.DeleteBookAsync(id);

        Assert.IsType<Result<BooksApiError>.Success>(result);
    }

    [Fact]
    public async Task DeleteBookAsync_ReturnsUnauthenticated_WhenResponseIsUnauthorized()
    {
        var id = Guid.NewGuid();
        var httpClient = CreateHttpClient(HttpStatusCode.Unauthorized, content: null);
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.DeleteBookAsync(id);

        var failure = Assert.IsType<Result<BooksApiError>.Failure>(result);
        Assert.Equal(BooksApiError.Unauthenticated, failure.Error);
    }

    [Fact]
    public async Task DeleteBookAsync_ReturnsNetworkError_WhenHttpCallThrows()
    {
        var id = Guid.NewGuid();
        var httpClient = CreateThrowingHttpClient(new HttpRequestException("network unreachable"));
        var sut = new BooksApiClient(httpClient, _logger);

        var result = await sut.DeleteBookAsync(id);

        var failure = Assert.IsType<Result<BooksApiError>.Failure>(result);
        Assert.Equal(BooksApiError.NetworkError, failure.Error);
    }

    [Fact]
    public async Task DeleteBookAsync_ThrowsOperationCanceled_WhenTokenIsAlreadyCancelled()
    {
        var id = Guid.NewGuid();
        var httpClient = CreateThrowingHttpClient(new OperationCanceledException());
        var sut = new BooksApiClient(httpClient, _logger);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.DeleteBookAsync(id, cts.Token));
    }
}
