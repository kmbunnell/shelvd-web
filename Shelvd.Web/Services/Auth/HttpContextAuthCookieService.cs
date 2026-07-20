using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Shelvd.Web.Services.Auth;

public sealed class HttpContextAuthCookieService(IHttpContextAccessor httpContextAccessor) : IAuthCookieService
{
    public Task SignInAsync(AuthSession session, AuthenticationProperties? properties = null)
    {
        var httpContext = RequireHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId),
            new Claim(ClaimTypes.Email, session.Email),
            new Claim(AuthClaimTypes.AccessToken, session.AccessToken),
            new Claim(AuthClaimTypes.RefreshToken, session.RefreshToken)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // SignInAsync only writes the response cookie; it doesn't update HttpContext.User for
        // the rest of this request (that's only populated during authentication, which already
        // ran). Set it explicitly so code later in the same request — e.g. after a mid-request
        // token refresh — sees the new claims instead of the stale ones from cookie auth.
        httpContext.User = principal;

        return httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    }

    public Task SignOutAsync() =>
        RequireHttpContext().SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    private HttpContext RequireHttpContext() =>
        httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No active HttpContext.");
}
