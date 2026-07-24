using System.Net;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Services.Common;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.Tags;

public sealed class TagsService(IHttpClientFactory httpClientFactory, ILogger<TagsService> logger) : ITagsService
{
    public const string SupabaseRestHttpClientName = "SupabaseRest";

    private readonly SupabaseRestClient _restClient = new(httpClientFactory);

    public Task<Result<IReadOnlyList<TagDto>, TagsError>> GetTagsAsync(string accessToken) =>
        _restClient.GetListAsync<TagDto, TagsError>(
            SupabaseRestHttpClientName,
            resourceName: "tags",
            path: "tags",
            query: new Dictionary<string, string?>
            {
                ["select"] = "id,name,is_default",
                ["order"] = "name.asc"
            },
            accessToken,
            logger,
            mapStatusError: statusCode => statusCode == HttpStatusCode.Unauthorized ? TagsError.Unauthenticated : TagsError.Unknown,
            malformedResponseError: TagsError.MalformedResponse,
            networkError: TagsError.NetworkError,
            unknownError: TagsError.Unknown);
}
