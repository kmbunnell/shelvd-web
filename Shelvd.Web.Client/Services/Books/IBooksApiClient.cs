using Shelvd.Web.Shared.Common;
using Shelvd.Web.Client.Models;

namespace Shelvd.Web.Client.Services.Books;

public interface IBooksApiClient
{
    Task<Result<IReadOnlyList<BookDto>, BooksApiError>> GetBooksAsync(CancellationToken cancellationToken = default);
}
