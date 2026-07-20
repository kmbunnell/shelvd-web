using Microsoft.AspNetCore.Authentication;

namespace Shelvd.Web.Services.Auth;

public interface IAuthCookieService
{
    Task SignInAsync(AuthSession session, AuthenticationProperties? properties = null);
    Task SignOutAsync();
}
