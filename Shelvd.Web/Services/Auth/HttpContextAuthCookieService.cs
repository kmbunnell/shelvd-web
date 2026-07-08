using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Shelvd.Web.Services.Auth;

public sealed class HttpContextAuthCookieService(IHttpContextAccessor httpContextAccessor) : IAuthCookieService
{
    public Task SignInAsync(AuthSession session)
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

        return httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    public Task SignOutAsync() =>
        RequireHttpContext().SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    private HttpContext RequireHttpContext() =>
        httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No active HttpContext.");
}
