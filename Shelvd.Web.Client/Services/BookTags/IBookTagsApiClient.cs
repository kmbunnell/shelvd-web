using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.BookTags;

public interface IBookTagsApiClient
{
    Task<Result<BookTagsApiError>> TagBookAsync(Guid bookId, Guid tagId, CancellationToken cancellationToken = default);

    Task<Result<BookTagsApiError>> UntagBookAsync(Guid bookId, Guid tagId, CancellationToken cancellationToken = default);
}
