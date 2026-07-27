using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.Books;

public interface IBooksApiClient
{
    Task<Result<IReadOnlyList<BookDto>, BooksApiError>> GetBooksAsync(CancellationToken cancellationToken = default);

    Task<Result<BookDto, BooksApiError>> GetBookAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<BooksApiError>> DeleteBookAsync(Guid id, CancellationToken cancellationToken = default);
}
