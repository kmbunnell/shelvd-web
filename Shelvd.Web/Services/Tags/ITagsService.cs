using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.Tags;

public interface ITagsService
{
    Task<Result<IReadOnlyList<TagDto>, TagsError>> GetTagsAsync(string accessToken);
}
