using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Services.Tags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Services.Tags;

public class TagsServiceTests
{
    private static readonly NullLogger<TagsService> _logger = NullLogger<TagsService>.Instance;

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
            .Setup(f => f.CreateClient(TagsService.SupabaseRestHttpClientName))
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
            .Setup(f => f.CreateClient(TagsService.SupabaseRestHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/rest/v1/") });
        return factory;
    }

    [Fact]
    public async Task GetTagsAsync_RequestsTagsWithBearerToken()
    {
        HttpRequestMessage? capturedRequest = null;
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, Array.Empty<object>(), request => capturedRequest = request);
        var sut = new TagsService(factory.Object, _logger);

        await sut.GetTagsAsync("access-token");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("/rest/v1/tags", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Contains("select=", capturedRequest.RequestUri.Query);
        Assert.Contains("order=name.asc", capturedRequest.RequestUri.Query);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("access-token", capturedRequest.Headers.Authorization?.Parameter);
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
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, responseTags);
        var sut = new TagsService(factory.Object, _logger);

        var result = await sut.GetTagsAsync("access-token");

        var success = Assert.IsType<Result<IReadOnlyList<TagDto>, TagsError>.Success>(result);
        var tag = Assert.Single(success.Value);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), tag.Id);
        Assert.Equal("Fantasy", tag.Name);
        Assert.False(tag.IsDefault);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsEmptyList_WhenResponseIsEmptyArray()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.OK, Array.Empty<object>());
        var sut = new TagsService(factory.Object, _logger);

        var result = await sut.GetTagsAsync("access-token");

        var success = Assert.IsType<Result<IReadOnlyList<TagDto>, TagsError>.Success>(result);
        Assert.Empty(success.Value);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsUnauthenticated_WhenResponseIsUnauthorized()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.Unauthorized, content: null);
        var sut = new TagsService(factory.Object, _logger);

        var result = await sut.GetTagsAsync("access-token");

        var failure = Assert.IsType<Result<IReadOnlyList<TagDto>, TagsError>.Failure>(result);
        Assert.Equal(TagsError.Unauthenticated, failure.Error);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsUnknown_WhenResponseIsNotSuccessStatusCodeAndNotUnauthorized()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.InternalServerError, content: null);
        var sut = new TagsService(factory.Object, _logger);

        var result = await sut.GetTagsAsync("access-token");

        var failure = Assert.IsType<Result<IReadOnlyList<TagDto>, TagsError>.Failure>(result);
        Assert.Equal(TagsError.Unknown, failure.Error);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsMalformedResponse_WhenBodyIsUnparsable()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<List<TagDto>?>(null)
            });
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(TagsService.SupabaseRestHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/rest/v1/") });
        var sut = new TagsService(factory.Object, _logger);

        var result = await sut.GetTagsAsync("access-token");

        var failure = Assert.IsType<Result<IReadOnlyList<TagDto>, TagsError>.Failure>(result);
        Assert.Equal(TagsError.MalformedResponse, failure.Error);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsNetworkError_WhenHttpCallThrows()
    {
        var factory = CreateThrowingHttpClientFactory(new HttpRequestException("network unreachable"));
        var sut = new TagsService(factory.Object, _logger);

        var result = await sut.GetTagsAsync("access-token");

        var failure = Assert.IsType<Result<IReadOnlyList<TagDto>, TagsError>.Failure>(result);
        Assert.Equal(TagsError.NetworkError, failure.Error);
    }
}
