using Shelvd.Web.Client.Models;
using Shelvd.Web.Services.Auth;
using Shelvd.Web.Services.Tags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Endpoints;

internal static class TagsEndpoints
{
    public static IEndpointRouteBuilder MapTagsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tags", async (HttpContext httpContext, ITagsService tagsService) =>
        {
            var accessToken = httpContext.GetAccessToken();
            if (accessToken is null)
            {
                return Results.Unauthorized();
            }

            var result = await tagsService.GetTagsAsync(accessToken);
            return result switch
            {
                Result<IReadOnlyList<TagDto>, TagsError>.Success success => Results.Ok(success.Value),
                Result<IReadOnlyList<TagDto>, TagsError>.Failure failure => failure.Error switch
                {
                    TagsError.Unauthenticated => Results.Unauthorized(),
                    _ => Results.StatusCode(StatusCodes.Status502BadGateway)
                },
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        })
        .RequireAuthorization();

        return app;
    }
}
