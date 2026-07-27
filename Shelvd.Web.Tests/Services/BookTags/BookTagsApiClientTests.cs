using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shelvd.Web.Client.Services.BookTags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Services.BookTags;

public class BookTagsApiClientTests
{
    private static readonly NullLogger<BookTagsApiClient> _logger = NullLogger<BookTagsApiClient>.Instance;
    private static readonly Guid _bookId = Guid.NewGuid();
    private static readonly Guid _tagId = Guid.NewGuid();

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, Action<HttpRequestMessage>? onRequest = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => onRequest?.Invoke(request))
            .ReturnsAsync(new HttpResponseMessage(statusCode));

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
    public async Task TagBookAsync_SendsPutToBookTagsRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var httpClient = CreateHttpClient(HttpStatusCode.NoContent, request => capturedRequest = request);
        var sut = new BookTagsApiClient(httpClient, _logger);

        await sut.TagBookAsync(_bookId, _tagId);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Put, capturedRequest!.Method);
        Assert.Equal($"/api/books/{_bookId}/tags/{_tagId}", capturedRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task TagBookAsync_ReturnsSuccess_WhenResponseIsSuccessful()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.NoContent);
        var sut = new BookTagsApiClient(httpClient, _logger);

        var result = await sut.TagBookAsync(_bookId, _tagId);

        Assert.IsType<Result<BookTagsApiError>.Success>(result);
    }

    [Fact]
    public async Task TagBookAsync_ReturnsUnauthenticated_WhenResponseIsUnauthorized()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.Unauthorized);
        var sut = new BookTagsApiClient(httpClient, _logger);

        var result = await sut.TagBookAsync(_bookId, _tagId);

        var failure = Assert.IsType<Result<BookTagsApiError>.Failure>(result);
        Assert.Equal(BookTagsApiError.Unauthenticated, failure.Error);
    }

    [Fact]
    public async Task TagBookAsync_ReturnsNetworkError_WhenHttpCallThrows()
    {
        var httpClient = CreateThrowingHttpClient(new HttpRequestException("network unreachable"));
        var sut = new BookTagsApiClient(httpClient, _logger);

        var result = await sut.TagBookAsync(_bookId, _tagId);

        var failure = Assert.IsType<Result<BookTagsApiError>.Failure>(result);
        Assert.Equal(BookTagsApiError.NetworkError, failure.Error);
    }

    [Fact]
    public async Task TagBookAsync_ThrowsOperationCanceled_WhenTokenIsAlreadyCancelled()
    {
        var httpClient = CreateThrowingHttpClient(new OperationCanceledException());
        var sut = new BookTagsApiClient(httpClient, _logger);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.TagBookAsync(_bookId, _tagId, cts.Token));
    }

    [Fact]
    public async Task UntagBookAsync_SendsDeleteToBookTagsRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var httpClient = CreateHttpClient(HttpStatusCode.NoContent, request => capturedRequest = request);
        var sut = new BookTagsApiClient(httpClient, _logger);

        await sut.UntagBookAsync(_bookId, _tagId);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);
        Assert.Equal($"/api/books/{_bookId}/tags/{_tagId}", capturedRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UntagBookAsync_ReturnsSuccess_WhenResponseIsSuccessful()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.NoContent);
        var sut = new BookTagsApiClient(httpClient, _logger);

        var result = await sut.UntagBookAsync(_bookId, _tagId);

        Assert.IsType<Result<BookTagsApiError>.Success>(result);
    }

    [Fact]
    public async Task UntagBookAsync_ReturnsNetworkError_WhenHttpCallThrows()
    {
        var httpClient = CreateThrowingHttpClient(new HttpRequestException("network unreachable"));
        var sut = new BookTagsApiClient(httpClient, _logger);

        var result = await sut.UntagBookAsync(_bookId, _tagId);

        var failure = Assert.IsType<Result<BookTagsApiError>.Failure>(result);
        Assert.Equal(BookTagsApiError.NetworkError, failure.Error);
    }
}
