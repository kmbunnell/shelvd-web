using Moq;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Client.Services.Tags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Services.Tags;

public class TagsLoaderTests
{
    private readonly Mock<ITagsCache> _tagsCache = new();

    [Fact]
    public async Task LoadAsync_PopulatesResultAndInvokesCallback_OnSuccess()
    {
        var tags = new List<TagDto> { new(Guid.NewGuid(), "Fantasy", false) };
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Success(tags));
        var sut = new TagsLoader(_tagsCache.Object);
        var callbackInvoked = false;

        await sut.LoadAsync(() => callbackInvoked = true);

        var success = Assert.IsType<Result<IReadOnlyList<TagDto>, TagsApiError>.Success>(sut.Result);
        Assert.Same(tags, success.Value);
        Assert.True(callbackInvoked);
    }

    [Fact]
    public async Task LoadAsync_PopulatesFailureResult_OnFailure()
    {
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<IReadOnlyList<TagDto>, TagsApiError>.Failure(TagsApiError.Unknown));
        var sut = new TagsLoader(_tagsCache.Object);

        await sut.LoadAsync(() => { });

        Assert.IsType<Result<IReadOnlyList<TagDto>, TagsApiError>.Failure>(sut.Result);
    }

    [Fact]
    public async Task LoadAsync_CancelsPriorInFlightLoad_WithoutThrowing()
    {
        var firstCallTcs = new TaskCompletionSource<Result<IReadOnlyList<TagDto>, TagsApiError>>();
        var capturedFirstToken = default(CancellationToken);
        var callCount = 0;
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                callCount++;
                if (callCount == 1)
                {
                    capturedFirstToken = ct;
                    return firstCallTcs.Task;
                }
                return Task.FromResult<Result<IReadOnlyList<TagDto>, TagsApiError>>(
                    new Result<IReadOnlyList<TagDto>, TagsApiError>.Success([]));
            });
        var sut = new TagsLoader(_tagsCache.Object);

        var firstLoad = sut.LoadAsync(() => { });
        var secondLoad = sut.LoadAsync(() => { });
        firstCallTcs.SetCanceled(capturedFirstToken);

        await firstLoad;
        await secondLoad;

        Assert.IsType<Result<IReadOnlyList<TagDto>, TagsApiError>.Success>(sut.Result);
    }

    [Fact]
    public void Dispose_CancelsInFlightLoad_WithoutThrowing()
    {
        var tcs = new TaskCompletionSource<Result<IReadOnlyList<TagDto>, TagsApiError>>();
        _tagsCache
            .Setup(c => c.GetTagsAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        var sut = new TagsLoader(_tagsCache.Object);
        _ = sut.LoadAsync(() => { });

        var exception = Record.Exception(sut.Dispose);

        Assert.Null(exception);
    }
}
