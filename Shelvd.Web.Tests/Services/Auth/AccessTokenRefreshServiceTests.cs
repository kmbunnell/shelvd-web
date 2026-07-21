using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shelvd.Web.Services.Auth;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Services.Auth;

public class AccessTokenRefreshServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly NullLogger<AccessTokenRefreshService> _logger = NullLogger<AccessTokenRefreshService>.Instance;

    private static string CreateToken(DateTime expiresUtc) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(expires: expiresUtc));

    private static (
        AccessTokenRefreshService Sut,
        Mock<IAuthService> AuthService,
        Mock<IAuthCookieService> AuthCookieService,
        Mock<IAuthenticationService> AuthenticationService,
        DefaultHttpContext HttpContext) CreateSut(FakeTimeProvider? timeProvider = null, IMemoryCache? cache = null)
    {
        var authService = new Mock<IAuthService>();
        var authCookieService = new Mock<IAuthCookieService>();
        var authenticationService = new Mock<IAuthenticationService>();
        var services = new ServiceCollection();
        services.AddSingleton(authenticationService.Object);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var sut = new AccessTokenRefreshService(
            authService.Object,
            authCookieService.Object,
            timeProvider ?? new FakeTimeProvider(_now),
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            _logger);

        return (sut, authService, authCookieService, authenticationService, httpContext);
    }

    private static void SetUser(DefaultHttpContext httpContext, string accessToken, string refreshToken)
    {
        var claims = new[]
        {
            new Claim(AuthClaimTypes.AccessToken, accessToken),
            new Claim(AuthClaimTypes.RefreshToken, refreshToken)
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    [Fact]
    public async Task RefreshIfNeededAsync_ReturnsNotNeeded_WhenUnauthenticated()
    {
        var (sut, authService, authCookieService, _, httpContext) = CreateSut();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await sut.RefreshIfNeededAsync(httpContext);

        Assert.Equal(TokenRefreshOutcome.NotNeeded, result);
        authService.Verify(a => a.RefreshSessionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        authCookieService.Verify(a => a.SignInAsync(It.IsAny<AuthSession>(), It.IsAny<AuthenticationProperties?>()), Times.Never);
        authCookieService.Verify(a => a.SignOutAsync(), Times.Never);
    }

    [Fact]
    public async Task RefreshIfNeededAsync_ReturnsNotNeeded_WhenAccessTokenIsStillFresh()
    {
        var (sut, authService, _, _, httpContext) = CreateSut();
        SetUser(httpContext, CreateToken(_now.UtcDateTime.AddMinutes(30)), Guid.NewGuid().ToString());

        var result = await sut.RefreshIfNeededAsync(httpContext);

        Assert.Equal(TokenRefreshOutcome.NotNeeded, result);
        authService.Verify(a => a.RefreshSessionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshIfNeededAsync_RefreshesAndReissuesCookie_WhenTokenIsStale()
    {
        var (sut, authService, authCookieService, _, httpContext) = CreateSut();
        var refreshToken = Guid.NewGuid().ToString();
        var staleAccessToken = CreateToken(_now.UtcDateTime.AddSeconds(10));
        SetUser(httpContext, staleAccessToken, refreshToken);
        var newSession = new AuthSession("user-1", "user@example.com", "new-access-token", "new-refresh-token");
        authService
            .Setup(a => a.RefreshSessionAsync(staleAccessToken, refreshToken))
            .ReturnsAsync(new Result<AuthSession, AuthError>.Success(newSession));

        var result = await sut.RefreshIfNeededAsync(httpContext);

        Assert.Equal(TokenRefreshOutcome.Refreshed, result);
        authService.Verify(a => a.RefreshSessionAsync(staleAccessToken, refreshToken), Times.Once);
        authCookieService.Verify(a => a.SignInAsync(
            newSession,
            It.Is<AuthenticationProperties>(p => p.IssuedUtc == _now && p.ExpiresUtc == _now.Add(AuthCookieOptions.ExpireTimeSpan))),
            Times.Once);
        authCookieService.Verify(a => a.SignOutAsync(), Times.Never);
    }

    [Fact]
    public async Task RefreshIfNeededAsync_StashesPreRefreshToken_SoLogoutCanClearTheCorrectCacheEntry()
    {
        // Regression test: SignInAsync overwrites httpContext.User with the rotated refresh
        // token before a same-request handler (e.g. POST /logout) runs, so /logout can no
        // longer read the pre-refresh token off the claim. It must find it via HttpContext.Items
        // instead to clear the cache entry that's actually keyed by the stale token.
        var (sut, authService, _, _, httpContext) = CreateSut();
        var staleRefreshToken = Guid.NewGuid().ToString();
        var staleAccessToken = CreateToken(_now.UtcDateTime.AddSeconds(10));
        SetUser(httpContext, staleAccessToken, staleRefreshToken);
        var newSession = new AuthSession("user-1", "user@example.com", "new-access-token", "new-refresh-token");
        authService
            .Setup(a => a.RefreshSessionAsync(staleAccessToken, staleRefreshToken))
            .ReturnsAsync(new Result<AuthSession, AuthError>.Success(newSession));

        await sut.RefreshIfNeededAsync(httpContext);

        Assert.Equal(staleRefreshToken, httpContext.Items["PreRefreshRefreshToken"]);
    }

    [Fact]
    public async Task RefreshIfNeededAsync_SignsOutAndChallenges_WhenRefreshFails()
    {
        var (sut, authService, authCookieService, authenticationService, httpContext) = CreateSut();
        var refreshToken = Guid.NewGuid().ToString();
        var staleAccessToken = CreateToken(_now.UtcDateTime.AddSeconds(10));
        SetUser(httpContext, staleAccessToken, refreshToken);
        authService
            .Setup(a => a.RefreshSessionAsync(staleAccessToken, refreshToken))
            .ReturnsAsync(new Result<AuthSession, AuthError>.Failure(AuthError.Unknown));

        var result = await sut.RefreshIfNeededAsync(httpContext);

        Assert.Equal(TokenRefreshOutcome.Failed, result);
        authCookieService.Verify(a => a.SignOutAsync(), Times.Once);
        authenticationService.Verify(a => a.ChallengeAsync(httpContext, CookieAuthenticationDefaults.AuthenticationScheme, It.IsAny<AuthenticationProperties?>()), Times.Once);
    }

    [Fact]
    public async Task RefreshIfNeededAsync_DedupesConcurrentCalls_ForTheSameSession()
    {
        var refreshToken = Guid.NewGuid().ToString();
        var staleAccessToken = CreateToken(_now.UtcDateTime.AddSeconds(10));
        var cache = new MemoryCache(new MemoryCacheOptions());
        // Needs to be a real, still-valid JWT: a waiter that reuses this cached session
        // re-checks AccessTokenExpiry.NeedsRefresh on it, and a non-JWT string always reads
        // as needing refresh, which would defeat the dedup this test is verifying.
        var newSession = new AuthSession("user-1", "user@example.com", CreateToken(_now.UtcDateTime.AddMinutes(30)), "new-refresh-token");

        var authService = new Mock<IAuthService>();
        var refreshGate = new TaskCompletionSource();
        authService
            .Setup(a => a.RefreshSessionAsync(staleAccessToken, refreshToken))
            .Returns(async () =>
            {
                await refreshGate.Task;
                return new Result<AuthSession, AuthError>.Success(newSession);
            });
        var authCookieService = new Mock<IAuthCookieService>();
        var authenticationService = new Mock<IAuthenticationService>();
        var services = new ServiceCollection();
        services.AddSingleton(authenticationService.Object);

        var contextA = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var contextB = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        SetUser(contextA, staleAccessToken, refreshToken);
        SetUser(contextB, staleAccessToken, refreshToken);

        var sut = new AccessTokenRefreshService(authService.Object, authCookieService.Object, new FakeTimeProvider(_now), cache, _logger);

        var taskA = sut.RefreshIfNeededAsync(contextA);
        var taskB = sut.RefreshIfNeededAsync(contextB);

        // Give the second call a chance to reach (and block on) the lock before the first
        // call's refresh completes, so both are genuinely racing rather than serialized.
        await Task.Delay(50);
        refreshGate.SetResult();
        var results = await Task.WhenAll(taskA, taskB);

        Assert.All(results, r => Assert.Equal(TokenRefreshOutcome.Refreshed, r));
        authService.Verify(a => a.RefreshSessionAsync(staleAccessToken, refreshToken), Times.Once);
        authCookieService.Verify(a => a.SignInAsync(newSession, It.IsAny<AuthenticationProperties>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RefreshIfNeededAsync_RefreshesAgain_AfterCacheEntryTtlExpires()
    {
        var timeProvider = new FakeTimeProvider(_now);
        var cache = new MemoryCache(new MemoryCacheOptions { Clock = new FakeMemoryCacheClock(timeProvider) });
        var refreshToken = Guid.NewGuid().ToString();
        var authService = new Mock<IAuthService>();
        var authCookieService = new Mock<IAuthCookieService>();
        var authenticationService = new Mock<IAuthenticationService>();
        var services = new ServiceCollection();
        services.AddSingleton(authenticationService.Object);
        var sut = new AccessTokenRefreshService(authService.Object, authCookieService.Object, timeProvider, cache, _logger);

        var firstStaleToken = CreateToken(_now.UtcDateTime.AddSeconds(10));
        var firstContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        SetUser(firstContext, firstStaleToken, refreshToken);
        var firstSession = new AuthSession("user-1", "user@example.com", "session-1-access", "session-1-refresh");
        authService
            .Setup(a => a.RefreshSessionAsync(firstStaleToken, refreshToken))
            .ReturnsAsync(new Result<AuthSession, AuthError>.Success(firstSession));

        await sut.RefreshIfNeededAsync(firstContext);

        // Advance well past the refresh cache's TTL so the cached session entry is evicted.
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        var secondStaleToken = CreateToken(timeProvider.GetUtcNow().UtcDateTime.AddSeconds(10));
        var secondContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        SetUser(secondContext, secondStaleToken, refreshToken);
        var secondSession = new AuthSession("user-1", "user@example.com", "session-2-access", "session-2-refresh");
        authService
            .Setup(a => a.RefreshSessionAsync(secondStaleToken, refreshToken))
            .ReturnsAsync(new Result<AuthSession, AuthError>.Success(secondSession));

        var result = await sut.RefreshIfNeededAsync(secondContext);

        Assert.Equal(TokenRefreshOutcome.Refreshed, result);
        authService.Verify(a => a.RefreshSessionAsync(secondStaleToken, refreshToken), Times.Once);
    }

    [Fact]
    public void ClearCachedSession_RemovesCachedEntry()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var refreshToken = Guid.NewGuid().ToString();
        var session = new AuthSession("user-1", "user@example.com", "access-token", refreshToken);
        cache.Set($"auth:refresh:session:{refreshToken}", session, TimeSpan.FromSeconds(30));
        var authService = new Mock<IAuthService>();
        var authCookieService = new Mock<IAuthCookieService>();
        var sut = new AccessTokenRefreshService(authService.Object, authCookieService.Object, new FakeTimeProvider(_now), cache, _logger);

        sut.ClearCachedSession(refreshToken);

        Assert.False(cache.TryGetValue($"auth:refresh:session:{refreshToken}", out _));
    }

    private sealed class FakeMemoryCacheClock(TimeProvider timeProvider) : Microsoft.Extensions.Internal.ISystemClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}
