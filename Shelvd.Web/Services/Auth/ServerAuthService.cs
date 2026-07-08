using Shelvd.Web.Common;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;

namespace Shelvd.Web.Services.Auth;

public sealed class ServerAuthService(
    IGotrueClient<User, Session> gotrueClient,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ServerAuthService> logger) : IAuthService
{
    public async Task<Result<AuthSession?, AuthError>> SignInAsync(string email, string password)
    {
        try
        {
            var session = await gotrueClient.SignInWithPassword(email, password);
            return session is null
                ? new Result<AuthSession?, AuthError>.Failure(AuthError.Unknown)
                : new Result<AuthSession?, AuthError>.Success(ToAuthSession(session));
        }
        catch (GotrueException ex)
        {
            return new Result<AuthSession?, AuthError>.Failure(GotrueExceptionMapper.Map(ex));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during sign-in for {Email}", Mask(email));
            return new Result<AuthSession?, AuthError>.Failure(AuthError.Unknown);
        }
    }

    public async Task<Result<AuthSession?, AuthError>> SignUpAsync(string email, string password)
    {
        try
        {
            var session = await gotrueClient.SignUp(email, password, null);
            return new Result<AuthSession?, AuthError>.Success(session is null ? null : ToAuthSession(session));
        }
        catch (GotrueException ex)
        {
            return new Result<AuthSession?, AuthError>.Failure(GotrueExceptionMapper.Map(ex));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during sign-up for {Email}", Mask(email));
            return new Result<AuthSession?, AuthError>.Failure(AuthError.Unknown);
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            var user = httpContextAccessor.HttpContext?.User;
            var accessToken = user?.FindFirst(AuthClaimTypes.AccessToken)?.Value;
            var refreshToken = user?.FindFirst(AuthClaimTypes.RefreshToken)?.Value;

            if (accessToken is not null && refreshToken is not null)
            {
                await gotrueClient.SetSession(accessToken, refreshToken);
            }

            await gotrueClient.SignOut(Constants.SignOutScope.Local);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during Gotrue sign-out; local session will still be cleared.");
        }
    }

    private static AuthSession ToAuthSession(Session session) =>
        new(session.User!.Id!, session.User.Email!, session.AccessToken!, session.RefreshToken!);

    private static string Mask(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex <= 1 ? "***" : $"{email[0]}***{email[atIndex..]}";
    }
}
