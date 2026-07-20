namespace Shelvd.Web.Services.Auth;

public enum TokenRefreshOutcome
{
    NotNeeded,
    Refreshed,
    Failed
}

public interface IAccessTokenRefreshService
{
    Task<TokenRefreshOutcome> RefreshIfNeededAsync(HttpContext httpContext);

    // Purges any cached session for this refresh token so a sign-out doesn't leave a
    // still-valid-looking AuthSession sitting in the refresh cache after the token
    // that keyed it has been revoked server-side.
    void ClearCachedSession(string refreshToken);
}
