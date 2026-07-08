namespace Shelvd.Web.Services.Auth;

public sealed record AuthSession(string UserId, string Email, string AccessToken, string RefreshToken);
