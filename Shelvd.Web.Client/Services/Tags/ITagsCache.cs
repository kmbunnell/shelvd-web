using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.Tags;

public interface ITagsCache
{
    Task<Result<IReadOnlyList<TagDto>, TagsApiError>> GetTagsAsync(CancellationToken cancellationToken = default);
}
