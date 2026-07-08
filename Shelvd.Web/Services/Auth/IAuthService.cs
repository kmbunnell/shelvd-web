using Shelvd.Web.Common;

namespace Shelvd.Web.Services.Auth;

/// <summary>
/// Auth mutations (sign-in/up/out) always go through the server implementation
/// (<c>ServerAuthService</c> in Shelvd.Web), which owns the cookie session and never
/// exposes tokens to JS. WASM holds no Supabase session or tokens at all — only
/// userId/email are persisted for <c>AuthenticationState</c>.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthSession?, AuthError>> SignInAsync(string email, string password);
    Task<Result<AuthSession?, AuthError>> SignUpAsync(string email, string password);
    Task SignOutAsync();
}
