using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.Common;

public sealed class SupabaseRestClient(IHttpClientFactory httpClientFactory)
{
    public async Task<Result<IReadOnlyList<TRaw>, TError>> GetListAsync<TRaw, TError>(
        string httpClientName,
        string resourceName,
        string path,
        IDictionary<string, string?> query,
        string accessToken,
        ILogger logger,
        Func<HttpStatusCode, TError> mapStatusError,
        TError malformedResponseError,
        TError networkError,
        TError unknownError)
    {
        try
        {
            var client = httpClientFactory.CreateClient(httpClientName);
            var url = QueryHelpers.AddQueryString(path, query);
            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) }
            };
            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Fetching {Resource} rejected by Supabase with status {StatusCode}.", resourceName, response.StatusCode);
                return new Result<IReadOnlyList<TRaw>, TError>.Failure(mapStatusError(response.StatusCode));
            }

            var items = await response.Content.ReadFromJsonAsync<List<TRaw>>();
            if (items is null)
            {
                logger.LogWarning("Fetching {Resource} returned an unparsable response from Supabase.", resourceName);
                return new Result<IReadOnlyList<TRaw>, TError>.Failure(malformedResponseError);
            }

            return new Result<IReadOnlyList<TRaw>, TError>.Success(items);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error while fetching {Resource}.", resourceName);
            return new Result<IReadOnlyList<TRaw>, TError>.Failure(networkError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while fetching {Resource}.", resourceName);
            return new Result<IReadOnlyList<TRaw>, TError>.Failure(unknownError);
        }
    }
}
