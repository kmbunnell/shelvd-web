using Shelvd.Web.Client.Models;
using Shelvd.Web.Services.Auth;
using Shelvd.Web.Services.Books;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Endpoints;

internal static class BooksEndpoints
{
    public static IEndpointRouteBuilder MapBooksEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/books", async (HttpContext httpContext, IBooksService booksService) =>
        {
            var accessToken = httpContext.GetAccessToken();
            if (accessToken is null)
            {
                return Results.Unauthorized();
            }

            var result = await booksService.GetBooksAsync(accessToken);
            return result switch
            {
                Result<IReadOnlyList<BookDto>, BooksError>.Success success => Results.Ok(success.Value),
                Result<IReadOnlyList<BookDto>, BooksError>.Failure failure => failure.Error switch
                {
                    BooksError.Unauthenticated => Results.Unauthorized(),
                    _ => Results.StatusCode(StatusCodes.Status502BadGateway)
                },
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        })
        .RequireAuthorization();

        app.MapGet("/api/books/{id:guid}", async (HttpContext httpContext, IBooksService booksService, Guid id) =>
        {
            var accessToken = httpContext.GetAccessToken();
            if (accessToken is null)
            {
                return Results.Unauthorized();
            }

            var result = await booksService.GetBookByIdAsync(accessToken, id);
            return result switch
            {
                Result<BookDto?, BooksError>.Success { Value: null } => Results.NotFound(),
                Result<BookDto?, BooksError>.Success success => Results.Ok(success.Value),
                Result<BookDto?, BooksError>.Failure failure => failure.Error switch
                {
                    BooksError.Unauthenticated => Results.Unauthorized(),
                    _ => Results.StatusCode(StatusCodes.Status502BadGateway)
                },
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        })
        .RequireAuthorization();

        return app;
    }
}
