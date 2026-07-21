using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.Books;

public sealed class BooksService(IHttpClientFactory httpClientFactory, ILogger<BooksService> logger) : IBooksService
{
    public const string SupabaseRestHttpClientName = "SupabaseRest";

    public async Task<Result<IReadOnlyList<BookDto>, BooksError>> GetBooksAsync(string accessToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(SupabaseRestHttpClientName);
            var url = QueryHelpers.AddQueryString("books", new Dictionary<string, string?>
            {
                ["select"] = "id,isbn,title,authors,cover_image_url,created_at",
                ["order"] = "created_at.desc"
            });
            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) }
            };
            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Fetching books rejected by Supabase with status {StatusCode}.", response.StatusCode);
                return new Result<IReadOnlyList<BookDto>, BooksError>.Failure(
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? BooksError.Unauthenticated
                        : BooksError.Unknown);
            }

            var books = await response.Content.ReadFromJsonAsync<List<BookDto>>();
            if (books is null)
            {
                logger.LogWarning("Fetching books returned an unparsable response from Supabase.");
                return new Result<IReadOnlyList<BookDto>, BooksError>.Failure(BooksError.MalformedResponse);
            }

            return new Result<IReadOnlyList<BookDto>, BooksError>.Success(books);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error while fetching books.");
            return new Result<IReadOnlyList<BookDto>, BooksError>.Failure(BooksError.NetworkError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while fetching books.");
            return new Result<IReadOnlyList<BookDto>, BooksError>.Failure(BooksError.Unknown);
        }
    }
}
