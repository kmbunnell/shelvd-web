using System.Net;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.BookTags;

public sealed class BookTagsApiClient(HttpClient httpClient, ILogger<BookTagsApiClient> logger) : IBookTagsApiClient
{
    public Task<Result<BookTagsApiError>> TagBookAsync(Guid bookId, Guid tagId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, bookId, tagId, cancellationToken);

    public Task<Result<BookTagsApiError>> UntagBookAsync(Guid bookId, Guid tagId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, bookId, tagId, cancellationToken);

    private async Task<Result<BookTagsApiError>> SendAsync(HttpMethod method, Guid bookId, Guid tagId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"api/books/{bookId}/tags/{tagId}");
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new Result<BookTagsApiError>.Failure(
                    response.StatusCode == HttpStatusCode.Unauthorized
                        ? BookTagsApiError.Unauthenticated
                        : BookTagsApiError.Unknown);
            }

            return new Result<BookTagsApiError>.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error while updating tag {TagId} on book {BookId}.", tagId, bookId);
            return new Result<BookTagsApiError>.Failure(BookTagsApiError.NetworkError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while updating tag {TagId} on book {BookId}.", tagId, bookId);
            return new Result<BookTagsApiError>.Failure(BookTagsApiError.Unknown);
        }
    }
}
