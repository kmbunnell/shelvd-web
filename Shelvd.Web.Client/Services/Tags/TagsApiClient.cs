using System.Net;
using System.Net.Http.Json;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Client.Services.Tags;

public sealed class TagsApiClient(HttpClient httpClient, ILogger<TagsApiClient> logger) : ITagsApiClient
{
    public async Task<Result<IReadOnlyList<TagDto>, TagsApiError>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("api/tags", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new Result<IReadOnlyList<TagDto>, TagsApiError>.Failure(
                    response.StatusCode == HttpStatusCode.Unauthorized
                        ? TagsApiError.Unauthenticated
                        : TagsApiError.Unknown);
            }

            var tags = await response.Content.ReadFromJsonAsync<List<TagDto>>(cancellationToken);
            return tags is null
                ? new Result<IReadOnlyList<TagDto>, TagsApiError>.Failure(TagsApiError.Unknown)
                : new Result<IReadOnlyList<TagDto>, TagsApiError>.Success(tags);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error while fetching tags.");
            return new Result<IReadOnlyList<TagDto>, TagsApiError>.Failure(TagsApiError.NetworkError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while fetching tags.");
            return new Result<IReadOnlyList<TagDto>, TagsApiError>.Failure(TagsApiError.Unknown);
        }
    }
}
