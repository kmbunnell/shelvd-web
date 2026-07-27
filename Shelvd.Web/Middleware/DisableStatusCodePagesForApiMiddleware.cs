using Microsoft.AspNetCore.Diagnostics;

namespace Shelvd.Web.Middleware;

// /not-found is a Razor Component page, which only accepts the HTTP methods Blazor itself
// uses (GET, POST for enhanced form navigation) — re-executing a PUT/DELETE /api request
// against it 405s, which then overwrites the real status code (e.g. 401) the API endpoint
// returned. Opt those requests out via the officially supported feature flag instead of
// skipping UseStatusCodePagesWithReExecute's registration, which changes middleware
// ordering in ways that broke unrelated status-code handling (e.g. POST /logout).
public sealed class DisableStatusCodePagesForApiMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (httpContext.Request.Path.StartsWithSegments("/api"))
        {
            var statusCodePagesFeature = httpContext.Features.Get<IStatusCodePagesFeature>();
            if (statusCodePagesFeature is not null)
            {
                statusCodePagesFeature.Enabled = false;
            }
        }

        await next(httpContext);
    }
}
