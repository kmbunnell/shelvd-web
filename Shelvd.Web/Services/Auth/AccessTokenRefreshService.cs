using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Services.Auth;

public sealed class AccessTokenRefreshService(
    IAuthService authService,
    IAuthCookieService authCookieService,
    TimeProvider timeProvider,
    IMemoryCache cache,
    ILogger<AccessTokenRefreshService> logger) : IAccessTokenRefreshService
{
    // Cached sessions only need to survive the short burst of concurrent requests racing to
    // refresh the same session; a TTL means a session that's never refreshed again doesn't
    // leak a cache entry for the app's lifetime.
    private static readonly TimeSpan _cacheEntryTtl = TimeSpan.FromSeconds(30);

    // Per-key locks are ref-counted and removed the instant the last holder releases them,
    // rather than on an IMemoryCache TTL: a TTL-based eviction can dispose a semaphore while a
    // slow refresh call still holds it, throwing ObjectDisposedException on Release and letting
    // a concurrent waiter start unserialized against a fresh replacement — reopening the
    // refresh-token-reuse race this lock exists to prevent. Static because the lock must be
    // shared across requests regardless of this service's own (scoped) DI lifetime.
    private static readonly Dictionary<string, RefCountedLock> _refreshLocks = [];
    private static readonly object _refreshLocksGate = new();

    public async Task<TokenRefreshOutcome> RefreshIfNeededAsync(HttpContext httpContext)
    {
        // UseAuthentication() has already run by the time this middleware executes, so
        // httpContext.User is already populated for the cookie scheme — no need to
        // re-authenticate.
        var user = httpContext.User;
        if (user.Identity is not { IsAuthenticated: true })
        {
            return TokenRefreshOutcome.NotNeeded;
        }

        var accessToken = user.FindFirst(AuthClaimTypes.AccessToken)?.Value;
        var refreshToken = user.FindFirst(AuthClaimTypes.RefreshToken)?.Value;
        if (accessToken is null || refreshToken is null || !AccessTokenExpiry.NeedsRefresh(accessToken, timeProvider))
        {
            return TokenRefreshOutcome.NotNeeded;
        }

        logger.LogInformation("Access token near expiry; attempting refresh.");

        // A refresh on this request rotates the refresh token and overwrites httpContext.User
        // (see HttpContextAuthCookieService.SignInAsync) before this request's handler runs.
        // If that handler is /logout, it would otherwise only see the new token and clear the
        // wrong cache entry, leaving the one keyed by this (stale) token to outlive logout.
        // Stash it so /logout can clear both.
        httpContext.Items["PreRefreshRefreshToken"] = refreshToken;

        // Guards concurrent refresh attempts for the same session: Supabase rotates the
        // refresh token on use, so two requests racing to refresh with the same stale
        // token would have the second one fail (and can trip GoTrue's reuse/theft
        // detection, revoking the whole session). Keyed by the refresh token read off
        // the incoming cookie: every concurrent request for the same session presents
        // the same (still-stale) refresh token, so it doubles as a session correlation
        // key without needing a dedicated claim.
        var lockKey = LockCacheKey(refreshToken);
        var lockEntry = RentLock(lockKey);
        try
        {
            await lockEntry.Semaphore.WaitAsync();
            try
            {
                // Each request only sees the cookie it arrived with, so a waiter can't detect a
                // sibling request's refresh just by re-reading its own cookie. The cache holds the
                // latest known-good session per original refresh token so a waiter can reuse it
                // instead of retrying with a refresh token that's already been rotated away.
                if (cache.TryGetValue(SessionCacheKey(refreshToken), out AuthSession? cached) &&
                    cached is not null &&
                    !AccessTokenExpiry.NeedsRefresh(cached.AccessToken, timeProvider))
                {
                    // A sibling request already refreshed this session while we waited.
                    await authCookieService.SignInAsync(cached, FreshProperties());
                    logger.LogInformation("Access token refresh satisfied by a concurrent request.");
                    return TokenRefreshOutcome.Refreshed;
                }

                // Refresh with whatever token is newest: the cached one if a prior
                // (now-superseded) refresh already rotated it, otherwise the cookie's.
                var result = await authService.RefreshSessionAsync(cached?.AccessToken ?? accessToken, cached?.RefreshToken ?? refreshToken);
                if (result is Result<AuthSession, AuthError>.Success success)
                {
                    cache.Set(SessionCacheKey(refreshToken), success.Value, _cacheEntryTtl);
                    await authCookieService.SignInAsync(success.Value, FreshProperties());
                    logger.LogInformation("Access token refreshed successfully.");
                    return TokenRefreshOutcome.Refreshed;
                }

                cache.Remove(SessionCacheKey(refreshToken));
                await authCookieService.SignOutAsync();
                await httpContext.ChallengeAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                logger.LogWarning("Access token refresh failed; session signed out.");
                return TokenRefreshOutcome.Failed;
            }
            finally
            {
                lockEntry.Semaphore.Release();
            }
        }
        finally
        {
            ReturnLock(lockKey, lockEntry);
        }
    }

    public void ClearCachedSession(string refreshToken) => cache.Remove(SessionCacheKey(refreshToken));

    private static string SessionCacheKey(string refreshToken) => $"auth:refresh:session:{refreshToken}";

    private static string LockCacheKey(string refreshToken) => $"auth:refresh:lock:{refreshToken}";

    private static RefCountedLock RentLock(string key)
    {
        lock (_refreshLocksGate)
        {
            if (!_refreshLocks.TryGetValue(key, out var entry))
            {
                entry = new RefCountedLock();
                _refreshLocks[key] = entry;
            }

            entry.RefCount++;
            return entry;
        }
    }

    private static void ReturnLock(string key, RefCountedLock entry)
    {
        lock (_refreshLocksGate)
        {
            entry.RefCount--;
            if (entry.RefCount == 0)
            {
                _refreshLocks.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private AuthenticationProperties FreshProperties() => new()
    {
        IssuedUtc = timeProvider.GetUtcNow(),
        ExpiresUtc = timeProvider.GetUtcNow().Add(AuthCookieOptions.ExpireTimeSpan)
    };

    private sealed class RefCountedLock
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount;
    }
}
