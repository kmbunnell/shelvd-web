namespace Shelvd.Web.Services.Auth;

public interface IAuthCookieService
{
    Task SignInAsync(AuthSession session);
    Task SignOutAsync();
}
