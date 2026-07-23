using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shelvd.Web.Client.Services.Tags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Services.Tags;

public class TagsApiClientTests
{
    private static readonly NullLogger<TagsApiClient> _logger = NullLogger<TagsApiClient>.Instance;

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
    public async Task GetTagsAsync_RequestsApiTags()
    {
        HttpRequestMessage? capturedRequest = null;
        var httpClient = CreateHttpClient(HttpStatusCode.OK, Array.Empty<object>(), request => capturedRequest = request);
        var sut = new TagsApiClient(httpClient, _logger);

        await sut.GetTagsAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("/api/tags", capturedRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsParsedTags_WhenResponseIsSuccessful()
    {
        var responseTags = new[]
        {
            new
            {
                id = "11111111-1111-1111-1111-111111111111",
                name = "Fantasy",
                is_default = false
            }
        };
        var httpClient = CreateHttpClient(HttpStatusCode.OK, responseTags);
        var sut = new TagsApiClient(httpClient, _logger);

        var result = await sut.GetTagsAsync();

        var success = Assert.IsType<Result<IReadOnlyList<Shelvd.Web.Client.Models.TagDto>, TagsApiError>.Success>(result);
        var tag = Assert.Single(success.Value);
        Assert.Equal("Fantasy", tag.Name);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsUnauthenticated_WhenResponseIsUnauthorized()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.Unauthorized, content: null);
        var sut = new TagsApiClient(httpClient, _logger);

        var result = await sut.GetTagsAsync();

        var failure = Assert.IsType<Result<IReadOnlyList<Shelvd.Web.Client.Models.TagDto>, TagsApiError>.Failure>(result);
        Assert.Equal(TagsApiError.Unauthenticated, failure.Error);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsNetworkError_WhenHttpCallThrows()
    {
        var httpClient = CreateThrowingHttpClient(new HttpRequestException("network unreachable"));
        var sut = new TagsApiClient(httpClient, _logger);

        var result = await sut.GetTagsAsync();

        var failure = Assert.IsType<Result<IReadOnlyList<Shelvd.Web.Client.Models.TagDto>, TagsApiError>.Failure>(result);
        Assert.Equal(TagsApiError.NetworkError, failure.Error);
    }

    [Fact]
    public async Task GetTagsAsync_ThrowsOperationCanceled_WhenTokenIsAlreadyCancelled()
    {
        var httpClient = CreateThrowingHttpClient(new OperationCanceledException());
        var sut = new TagsApiClient(httpClient, _logger);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.GetTagsAsync(cts.Token));
    }
}
