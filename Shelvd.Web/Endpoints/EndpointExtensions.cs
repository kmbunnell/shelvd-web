using Shelvd.Web.Services.Auth;

namespace Shelvd.Web.Endpoints;

internal static class EndpointExtensions
{
    public static string? GetAccessToken(this HttpContext httpContext) =>
        httpContext.User.FindFirst(AuthClaimTypes.AccessToken)?.Value;
}
