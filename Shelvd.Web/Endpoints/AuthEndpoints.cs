using Microsoft.AspNetCore.Antiforgery;
using Shelvd.Web.Services.Auth;

namespace Shelvd.Web.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/logout", async (
            HttpContext httpContext,
            IAuthService authService,
            IAuthCookieService authCookieService,
            IAccessTokenRefreshService refreshService) =>
        {
            // A refresh that lands on this same request rotates the refresh token before this handler
            // runs, so the claim below may already be the new token while the refresh cache entry is
            // still keyed by the pre-refresh one (see AccessTokenRefreshService). Clear both.
            var currentRefreshToken = httpContext.User.FindFirst(AuthClaimTypes.RefreshToken)?.Value;
            var preRefreshToken = httpContext.Items["PreRefreshRefreshToken"] as string;
            if (currentRefreshToken is not null)
            {
                refreshService.ClearCachedSession(currentRefreshToken);
            }
            if (preRefreshToken is not null && preRefreshToken != currentRefreshToken)
            {
                refreshService.ClearCachedSession(preRefreshToken);
            }

            // Clear the local cookie first — it must not stay behind if the Gotrue call below fails.
            await authCookieService.SignOutAsync();
            await authService.SignOutAsync();
            return Results.LocalRedirect("/");
        })
        .RequireAuthorization()
        .AddEndpointFilter(async (context, next) =>
        {
            // UseAntiforgery() only validates endpoints carrying IAntiforgeryMetadata, which
            // minimal API endpoints don't get automatically (unlike Razor Components form
            // handlers). Validate explicitly so the <AntiforgeryToken /> in NavMenu.razor is
            // actually enforced, not just rendered.
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }

            return await next(context);
        });

        return app;
    }
}
