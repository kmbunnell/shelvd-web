using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.Tags;

public sealed class TagsCache(ITagsApiClient tagsApiClient) : ITagsCache
{
    private Result<IReadOnlyList<TagDto>, TagsApiError>? _cachedResult;

    public async Task<Result<IReadOnlyList<TagDto>, TagsApiError>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedResult is not null)
        {
            return _cachedResult;
        }

        var result = await tagsApiClient.GetTagsAsync(cancellationToken);
        if (result is Result<IReadOnlyList<TagDto>, TagsApiError>.Success)
        {
            _cachedResult = result;
        }

        return result;
    }
}
