using Shelvd.Web.Services.Auth;

namespace Shelvd.Web.Middleware;

public sealed class AccessTokenRefreshMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IAccessTokenRefreshService refreshService)
    {
        var outcome = await refreshService.RefreshIfNeededAsync(httpContext);
        if (outcome != TokenRefreshOutcome.Failed)
        {
            await next(httpContext);
        }
    }
}
