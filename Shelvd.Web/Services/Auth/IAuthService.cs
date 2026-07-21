using Shelvd.Web.Shared.Common;

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

    // Unlike SignInAsync/SignUpAsync, the success case is never null here: a refresh either
    // yields a usable session or is a Failure — there's no "succeeded but no session" state
    // (that only applies to sign-up's email-confirmation flow).
    Task<Result<AuthSession, AuthError>> RefreshSessionAsync(string accessToken, string refreshToken);
    Task SignOutAsync();
}
