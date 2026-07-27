using Shelvd.Web.Services.BookTags;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Endpoints;

internal static class BookTagsEndpoints
{
    public static IEndpointRouteBuilder MapBookTagsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/books/{bookId:guid}/tags/{tagId:guid}", async (HttpContext httpContext, IBookTagsService bookTagsService, Guid bookId, Guid tagId) =>
        {
            var accessToken = httpContext.GetAccessToken();
            if (accessToken is null)
            {
                return Results.Unauthorized();
            }

            var result = await bookTagsService.TagBookAsync(accessToken, bookId, tagId);
            return MapResult(result);
        })
        .RequireAuthorization();

        app.MapDelete("/api/books/{bookId:guid}/tags/{tagId:guid}", async (HttpContext httpContext, IBookTagsService bookTagsService, Guid bookId, Guid tagId) =>
        {
            var accessToken = httpContext.GetAccessToken();
            if (accessToken is null)
            {
                return Results.Unauthorized();
            }

            var result = await bookTagsService.UntagBookAsync(accessToken, bookId, tagId);
            return MapResult(result);
        })
        .RequireAuthorization();

        return app;
    }

    private static IResult MapResult(Result<BookTagsError> result) => result switch
    {
        Result<BookTagsError>.Success => Results.NoContent(),
        Result<BookTagsError>.Failure failure => failure.Error switch
        {
            BookTagsError.Unauthenticated => Results.Unauthorized(),
            _ => Results.StatusCode(StatusCodes.Status502BadGateway)
        },
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
}
