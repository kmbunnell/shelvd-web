using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shelvd.Web.Services.BookTags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Services.BookTags;

public class BookTagsServiceTests
{
    private static readonly NullLogger<BookTagsService> _logger = NullLogger<BookTagsService>.Instance;
    private static readonly Guid _bookId = Guid.NewGuid();
    private static readonly Guid _tagId = Guid.NewGuid();

    private static Mock<IHttpClientFactory> CreateHttpClientFactory(
        HttpStatusCode statusCode,
        Action<HttpRequestMessage>? onRequest = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => onRequest?.Invoke(request))
            .ReturnsAsync(new HttpResponseMessage(statusCode));

        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(BookTagsService.SupabaseRestHttpClientName))
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
            .Setup(f => f.CreateClient(BookTagsService.SupabaseRestHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/rest/v1/") });
        return factory;
    }

    [Fact]
    public async Task TagBookAsync_PostsBookTagWithBearerTokenAndIgnoreDuplicatesPreference()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var factory = CreateHttpClientFactory(HttpStatusCode.Created, request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        });
        var sut = new BookTagsService(factory.Object, _logger);

        await sut.TagBookAsync("access-token", _bookId, _tagId);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("/rest/v1/book_tags", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("access-token", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Contains("resolution=ignore-duplicates", capturedRequest.Headers.GetValues("Prefer").Single());
        var body = JsonSerializer.Deserialize<Dictionary<string, Guid>>(capturedBody!);
        Assert.Equal(_bookId, body!["book_id"]);
        Assert.Equal(_tagId, body["tag_id"]);
    }

    [Fact]
    public async Task TagBookAsync_ReturnsSuccess_WhenResponseIsSuccessful()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.Created);
        var sut = new BookTagsService(factory.Object, _logger);

        var result = await sut.TagBookAsync("access-token", _bookId, _tagId);

        Assert.IsType<Result<BookTagsError>.Success>(result);
    }

    [Fact]
    public async Task TagBookAsync_ReturnsUnauthenticated_WhenResponseIsUnauthorized()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.Unauthorized);
        var sut = new BookTagsService(factory.Object, _logger);

        var result = await sut.TagBookAsync("access-token", _bookId, _tagId);

        var failure = Assert.IsType<Result<BookTagsError>.Failure>(result);
        Assert.Equal(BookTagsError.Unauthenticated, failure.Error);
    }

    [Fact]
    public async Task TagBookAsync_ReturnsUnknown_WhenResponseIsNotSuccessStatusCodeAndNotUnauthorized()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.InternalServerError);
        var sut = new BookTagsService(factory.Object, _logger);

        var result = await sut.TagBookAsync("access-token", _bookId, _tagId);

        var failure = Assert.IsType<Result<BookTagsError>.Failure>(result);
        Assert.Equal(BookTagsError.Unknown, failure.Error);
    }

    [Fact]
    public async Task TagBookAsync_ReturnsNetworkError_WhenHttpCallThrows()
    {
        var factory = CreateThrowingHttpClientFactory(new HttpRequestException("network unreachable"));
        var sut = new BookTagsService(factory.Object, _logger);

        var result = await sut.TagBookAsync("access-token", _bookId, _tagId);

        var failure = Assert.IsType<Result<BookTagsError>.Failure>(result);
        Assert.Equal(BookTagsError.NetworkError, failure.Error);
    }

    [Fact]
    public async Task UntagBookAsync_DeletesBookTagWithBookIdAndTagIdFilters()
    {
        HttpRequestMessage? capturedRequest = null;
        var factory = CreateHttpClientFactory(HttpStatusCode.NoContent, request => capturedRequest = request);
        var sut = new BookTagsService(factory.Object, _logger);

        await sut.UntagBookAsync("access-token", _bookId, _tagId);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);
        Assert.Equal("/rest/v1/book_tags", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Contains($"book_id=eq.{_bookId}", capturedRequest.RequestUri.Query);
        Assert.Contains($"tag_id=eq.{_tagId}", capturedRequest.RequestUri.Query);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task UntagBookAsync_ReturnsSuccess_WhenResponseIsNoContent()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.NoContent);
        var sut = new BookTagsService(factory.Object, _logger);

        var result = await sut.UntagBookAsync("access-token", _bookId, _tagId);

        Assert.IsType<Result<BookTagsError>.Success>(result);
    }

    [Fact]
    public async Task UntagBookAsync_ReturnsUnauthenticated_WhenResponseIsUnauthorized()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.Unauthorized);
        var sut = new BookTagsService(factory.Object, _logger);

        var result = await sut.UntagBookAsync("access-token", _bookId, _tagId);

        var failure = Assert.IsType<Result<BookTagsError>.Failure>(result);
        Assert.Equal(BookTagsError.Unauthenticated, failure.Error);
    }

    [Fact]
    public async Task UntagBookAsync_ReturnsNetworkError_WhenHttpCallThrows()
    {
        var factory = CreateThrowingHttpClientFactory(new HttpRequestException("network unreachable"));
        var sut = new BookTagsService(factory.Object, _logger);

        var result = await sut.UntagBookAsync("access-token", _bookId, _tagId);

        var failure = Assert.IsType<Result<BookTagsError>.Failure>(result);
        Assert.Equal(BookTagsError.NetworkError, failure.Error);
    }
}
