using Moq;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Client.Services.Tags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Services.Tags;

public class TagsCacheTests
{
    private readonly Mock<ITagsApiClient> _tagsApiClient = new();

    [Fact]
    public async Task GetTagsAsync_ReturnsResultFromApiClient_OnFirstCall()
    {
        var tags = new List<TagDto> { new(Guid.NewGuid(), "Fantasy", false) };
        _tagsApiClient
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success(tags));
        var sut = new TagsCache(_tagsApiClient.Object);

        var result = await sut.GetTagsAsync();

        var success = Assert.IsType<Result<IReadOnlyList<TagDto>, TagsApiError>.Success>(result);
        Assert.Same(tags, success.Value);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsCachedSuccess_WithoutCallingApiClientAgain()
    {
        var tags = new List<TagDto> { new(Guid.NewGuid(), "Fantasy", false) };
        _tagsApiClient
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success(tags));
        var sut = new TagsCache(_tagsApiClient.Object);

        await sut.GetTagsAsync();
        await sut.GetTagsAsync();

        _tagsApiClient.Verify(c => c.GetTagsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTagsAsync_RetriesApiClient_WhenPreviousCallFailed()
    {
        _tagsApiClient
            .SetupSequence(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Failure(TagsApiError.NetworkError))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([]));
        var sut = new TagsCache(_tagsApiClient.Object);

        var first = await sut.GetTagsAsync();
        var second = await sut.GetTagsAsync();

        Assert.IsType<Result<IReadOnlyList<TagDto>, TagsApiError>.Failure>(first);
        Assert.IsType<Result<IReadOnlyList<TagDto>, TagsApiError>.Success>(second);
        _tagsApiClient.Verify(c => c.GetTagsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetTagsAsync_PassesCancellationTokenThrough()
    {
        _tagsApiClient
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([]));
        var sut = new TagsCache(_tagsApiClient.Object);
        using var cts = new CancellationTokenSource();

        await sut.GetTagsAsync(cts.Token);

        _tagsApiClient.Verify(c => c.GetTagsAsync(cts.Token), Times.Once);
    }
}
