using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.BookTags;

public interface IBookTagsService
{
    Task<Result<BookTagsError>> TagBookAsync(string accessToken, Guid bookId, Guid tagId);

    Task<Result<BookTagsError>> UntagBookAsync(string accessToken, Guid bookId, Guid tagId);
}
