using System.Net;
using Shelvd.Web.Services.Common;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.BookTags;

public sealed class BookTagsService(IHttpClientFactory httpClientFactory, ILogger<BookTagsService> logger) : IBookTagsService
{
    public const string SupabaseRestHttpClientName = "SupabaseRest";

    private readonly SupabaseRestClient _restClient = new(httpClientFactory);

    public Task<Result<BookTagsError>> TagBookAsync(string accessToken, Guid bookId, Guid tagId) =>
        _restClient.PostAsync<object, BookTagsError>(
            SupabaseRestHttpClientName,
            resourceName: "book_tags",
            path: "book_tags",
            body: new { book_id = bookId, tag_id = tagId },
            accessToken,
            logger,
            mapStatusError: statusCode => statusCode == HttpStatusCode.Unauthorized ? BookTagsError.Unauthenticated : BookTagsError.Unknown,
            networkError: BookTagsError.NetworkError,
            unknownError: BookTagsError.Unknown,
            preferHeader: "resolution=ignore-duplicates,return=minimal");

    // PostgREST returns 204 No Content for a DELETE that matches zero rows, so an
    // already-untagged book never surfaces as an error here — no special-casing needed.
    public Task<Result<BookTagsError>> UntagBookAsync(string accessToken, Guid bookId, Guid tagId) =>
        _restClient.DeleteAsync<BookTagsError>(
            SupabaseRestHttpClientName,
            resourceName: "book_tags",
            path: "book_tags",
            query: new Dictionary<string, string?>
            {
                ["book_id"] = $"eq.{bookId}",
                ["tag_id"] = $"eq.{tagId}"
            },
            accessToken,
            logger,
            mapStatusError: statusCode => statusCode == HttpStatusCode.Unauthorized ? BookTagsError.Unauthenticated : BookTagsError.Unknown,
            networkError: BookTagsError.NetworkError,
            unknownError: BookTagsError.Unknown);
}
