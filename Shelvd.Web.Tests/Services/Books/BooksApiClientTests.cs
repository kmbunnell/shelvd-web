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
}
