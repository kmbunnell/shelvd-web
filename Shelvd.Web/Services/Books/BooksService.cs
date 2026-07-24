using System.Net;
using System.Text.Json.Serialization;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Services.Common;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.Books;

public sealed class BooksService(IHttpClientFactory httpClientFactory, ILogger<BooksService> logger) : IBooksService
{
    public const string SupabaseRestHttpClientName = "SupabaseRest";

    private readonly SupabaseRestClient _restClient = new(httpClientFactory);

    public async Task<Result<IReadOnlyList<BookDto>, BooksError>> GetBooksAsync(string accessToken)
    {
        var result = await QueryBooksAsync(accessToken, new Dictionary<string, string?>
        {
            ["order"] = "created_at.desc"
        });

        return result switch
        {
            Result<IReadOnlyList<RawBookDto>, BooksError>.Success success =>
                new Result<IReadOnlyList<BookDto>, BooksError>.Success(success.Value.Select(MapToBookDto).ToList()),
            Result<IReadOnlyList<RawBookDto>, BooksError>.Failure failure =>
                new Result<IReadOnlyList<BookDto>, BooksError>.Failure(failure.Error),
            _ => throw new InvalidOperationException("Unreachable Result variant.")
        };
    }

    public async Task<Result<BookDto?, BooksError>> GetBookByIdAsync(string accessToken, Guid id)
    {
        var result = await QueryBooksAsync(accessToken, new Dictionary<string, string?>
        {
            ["id"] = $"eq.{id}"
        });

        return result switch
        {
            Result<IReadOnlyList<RawBookDto>, BooksError>.Success success =>
                new Result<BookDto?, BooksError>.Success(success.Value.Select(MapToBookDto).FirstOrDefault()),
            Result<IReadOnlyList<RawBookDto>, BooksError>.Failure failure =>
                new Result<BookDto?, BooksError>.Failure(failure.Error),
            _ => throw new InvalidOperationException("Unreachable Result variant.")
        };
    }

    private Task<Result<IReadOnlyList<RawBookDto>, BooksError>> QueryBooksAsync(
        string accessToken, IDictionary<string, string?> extraQuery)
    {
        var query = new Dictionary<string, string?>(extraQuery)
        {
            ["select"] = "id,isbn,title,authors,cover_image_url,created_at,book_tags(tags(id,name,is_default))"
        };

        return _restClient.GetListAsync<RawBookDto, BooksError>(
            SupabaseRestHttpClientName,
            resourceName: "books",
            path: "books",
            query,
            accessToken,
            logger,
            mapStatusError: statusCode => statusCode == HttpStatusCode.Unauthorized ? BooksError.Unauthenticated : BooksError.Unknown,
            malformedResponseError: BooksError.MalformedResponse,
            networkError: BooksError.NetworkError,
            unknownError: BooksError.Unknown);
    }

    private static BookDto MapToBookDto(RawBookDto raw) => new(
        raw.Id,
        raw.Isbn,
        raw.Title,
        raw.Authors,
        raw.CoverImageUrl,
        raw.CreatedAt,
        (raw.BookTags ?? [])
            .Select(bookTag => bookTag.Tags)
            .Where(tag => tag is not null)
            .Select(tag => tag!)
            .ToList());

    // PostgREST's book_tags(tags(...)) embed shape is `book_tags: [{ tags: {...} }]`, not a
    // flat tag list, so this raw shape captures the wire response as-is before MapToBookDto
    // flattens it into BookDto.Tags — BookDto itself stays flat and client-friendly.
    // Tags is nullable because RLS can hide the referenced tag row while book_tags still
    // references it, in which case PostgREST embeds `tags: null`.
    private sealed record RawBookDto(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("isbn")] string? Isbn,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("authors")] IReadOnlyList<string> Authors,
        [property: JsonPropertyName("cover_image_url")] string? CoverImageUrl,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("book_tags")] IReadOnlyList<RawBookTag>? BookTags);

    private sealed record RawBookTag([property: JsonPropertyName("tags")] TagDto? Tags);
}
