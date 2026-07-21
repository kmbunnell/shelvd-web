using System.Net;
using System.Net.Http.Json;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.Books;

public sealed class BooksApiClient(HttpClient httpClient) : IBooksApiClient
{
    public async Task<Result<IReadOnlyList<BookDto>, BooksApiError>> GetBooksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("api/books", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new Result<IReadOnlyList<BookDto>, BooksApiError>.Failure(
                    response.StatusCode == HttpStatusCode.Unauthorized
                        ? BooksApiError.Unauthenticated
                        : BooksApiError.Unknown);
            }

            var books = await response.Content.ReadFromJsonAsync<List<BookDto>>(cancellationToken);
            return books is null
                ? new Result<IReadOnlyList<BookDto>, BooksApiError>.Failure(BooksApiError.Unknown)
                : new Result<IReadOnlyList<BookDto>, BooksApiError>.Success(books);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return new Result<IReadOnlyList<BookDto>, BooksApiError>.Failure(BooksApiError.NetworkError);
        }
        catch (Exception)
        {
            return new Result<IReadOnlyList<BookDto>, BooksApiError>.Failure(BooksApiError.Unknown);
        }
    }
}
