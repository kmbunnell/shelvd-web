using System.IdentityModel.Tokens.Jwt;

namespace Shelvd.Web.Services.Auth;

public static class AccessTokenExpiry
{
    private static readonly TimeSpan _refreshBuffer = TimeSpan.FromSeconds(60);

    public static bool NeedsRefresh(string accessToken, TimeProvider timeProvider)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
        {
            return true;
        }

        var token = handler.ReadJwtToken(accessToken);
        return token.ValidTo - timeProvider.GetUtcNow() <= _refreshBuffer;
    }
}
