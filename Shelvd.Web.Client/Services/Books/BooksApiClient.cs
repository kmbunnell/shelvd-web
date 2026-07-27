using System.Net;
using System.Net.Http.Json;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.Books;

public sealed class BooksApiClient(HttpClient httpClient, ILogger<BooksApiClient> logger) : IBooksApiClient
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
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error while fetching books.");
            return new Result<IReadOnlyList<BookDto>, BooksApiError>.Failure(BooksApiError.NetworkError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while fetching books.");
            return new Result<IReadOnlyList<BookDto>, BooksApiError>.Failure(BooksApiError.Unknown);
        }
    }

    public async Task<Result<BookDto, BooksApiError>> GetBookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync($"api/books/{id}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new Result<BookDto, BooksApiError>.Failure(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => BooksApiError.Unauthenticated,
                    HttpStatusCode.NotFound => BooksApiError.NotFound,
                    _ => BooksApiError.Unknown
                });
            }

            var book = await response.Content.ReadFromJsonAsync<BookDto>(cancellationToken);
            return book is null
                ? new Result<BookDto, BooksApiError>.Failure(BooksApiError.Unknown)
                : new Result<BookDto, BooksApiError>.Success(book);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error while fetching book {BookId}.", id);
            return new Result<BookDto, BooksApiError>.Failure(BooksApiError.NetworkError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while fetching book {BookId}.", id);
            return new Result<BookDto, BooksApiError>.Failure(BooksApiError.Unknown);
        }
    }

    public async Task<Result<BooksApiError>> DeleteBookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.DeleteAsync($"api/books/{id}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new Result<BooksApiError>.Failure(
                    response.StatusCode == HttpStatusCode.Unauthorized
                        ? BooksApiError.Unauthenticated
                        : BooksApiError.Unknown);
            }

            return new Result<BooksApiError>.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error while deleting book {BookId}.", id);
            return new Result<BooksApiError>.Failure(BooksApiError.NetworkError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while deleting book {BookId}.", id);
            return new Result<BooksApiError>.Failure(BooksApiError.Unknown);
        }
    }
}
