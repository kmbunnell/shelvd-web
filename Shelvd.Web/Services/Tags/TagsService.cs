using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.Tags;

public sealed class TagsService(IHttpClientFactory httpClientFactory, ILogger<TagsService> logger) : ITagsService
{
    public const string SupabaseRestHttpClientName = "SupabaseRest";

    public async Task<Result<IReadOnlyList<TagDto>, TagsError>> GetTagsAsync(string accessToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(SupabaseRestHttpClientName);
            var url = QueryHelpers.AddQueryString("tags", new Dictionary<string, string?>
            {
                ["select"] = "id,name,is_default",
                ["order"] = "name.asc"
            });
            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) }
            };
            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Fetching tags rejected by Supabase with status {StatusCode}.", response.StatusCode);
                return new Result<IReadOnlyList<TagDto>, TagsError>.Failure(
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                        ? TagsError.Unauthenticated
                        : TagsError.Unknown);
            }

            var tags = await response.Content.ReadFromJsonAsync<List<TagDto>>();
            if (tags is null)
            {
                logger.LogWarning("Fetching tags returned an unparsable response from Supabase.");
                return new Result<IReadOnlyList<TagDto>, TagsError>.Failure(TagsError.MalformedResponse);
            }

            return new Result<IReadOnlyList<TagDto>, TagsError>.Success(tags);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error while fetching tags.");
            return new Result<IReadOnlyList<TagDto>, TagsError>.Failure(TagsError.NetworkError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while fetching tags.");
            return new Result<IReadOnlyList<TagDto>, TagsError>.Failure(TagsError.Unknown);
        }
    }
}
