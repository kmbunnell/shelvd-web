using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Time.Testing;
using Shelvd.Web.Services.Auth;

namespace Shelvd.Web.Tests.Services.Auth;

public class AccessTokenExpiryTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private static string CreateToken(DateTime expiresUtc)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(expires: expiresUtc);
        return handler.WriteToken(token);
    }

    [Fact]
    public void NeedsRefresh_ReturnsFalse_WhenTokenExpiresWellInFuture()
    {
        var timeProvider = new FakeTimeProvider(_now);
        var token = CreateToken(_now.UtcDateTime.AddMinutes(30));

        var result = AccessTokenExpiry.NeedsRefresh(token, timeProvider);

        Assert.False(result);
    }

    [Fact]
    public void NeedsRefresh_ReturnsTrue_WhenTokenExpiresInsideBuffer()
    {
        var timeProvider = new FakeTimeProvider(_now);
        var token = CreateToken(_now.UtcDateTime.AddSeconds(30));

        var result = AccessTokenExpiry.NeedsRefresh(token, timeProvider);

        Assert.True(result);
    }

    [Fact]
    public void NeedsRefresh_ReturnsTrue_WhenTokenAlreadyExpired()
    {
        var timeProvider = new FakeTimeProvider(_now);
        var token = CreateToken(_now.UtcDateTime.AddMinutes(-5));

        var result = AccessTokenExpiry.NeedsRefresh(token, timeProvider);

        Assert.True(result);
    }

    [Fact]
    public void NeedsRefresh_ReturnsTrue_WhenTokenIsMalformed()
    {
        var timeProvider = new FakeTimeProvider(_now);

        var result = AccessTokenExpiry.NeedsRefresh("not-a-jwt", timeProvider);

        Assert.True(result);
    }
}
