using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.Tags;

public interface ITagsApiClient
{
    Task<Result<IReadOnlyList<TagDto>, TagsApiError>> GetTagsAsync(CancellationToken cancellationToken = default);
}
