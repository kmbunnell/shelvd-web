using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.Books;

public interface IBooksService
{
    Task<Result<IReadOnlyList<BookDto>, BooksError>> GetBooksAsync(string accessToken);
}
