using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Shelvd.Web.Shared.Common;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;

namespace Shelvd.Web.Services.Auth;

public sealed class ServerAuthService(
    IGotrueClient<User, Session> gotrueClient,
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ServerAuthService> logger) : IAuthService
{
    // Name of the HttpClient (registered in Program.cs) used only for the refresh REST call
    // below — see RefreshSessionAsync for why refresh bypasses the shared IGotrueClient.
    public const string GotrueHttpClientName = "Gotrue";

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
            logger.LogInformation(ex, "Sign-in rejected for {Email}: {Reason}", Mask(email), ex.Reason);
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
            logger.LogInformation(ex, "Sign-up rejected for {Email}: {Reason}", Mask(email), ex.Reason);
            return new Result<AuthSession?, AuthError>.Failure(GotrueExceptionMapper.Map(ex));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during sign-up for {Email}", Mask(email));
            return new Result<AuthSession?, AuthError>.Failure(AuthError.Unknown);
        }
    }

    // Goes straight to the GoTrue REST refresh endpoint via a plain HttpClient rather than
    // gotrueClient.SetSession: IGotrueClient is registered as a singleton (it must be, to
    // share cookie-independent config across requests), so calling SetSession on it mutates
    // that one shared client's internal "current session" from every concurrent request's
    // refresh — a data race. The refresh token in the request is enough on its own; no
    // client-side session state is needed to exchange it.
    public async Task<Result<AuthSession, AuthError>> RefreshSessionAsync(string accessToken, string refreshToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(GotrueHttpClientName);
            using var response = await client.PostAsJsonAsync(
                "token?grant_type=refresh_token",
                new GotrueRefreshRequest(refreshToken));

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Access-token refresh rejected by Supabase with status {StatusCode}.", response.StatusCode);
                return new Result<AuthSession, AuthError>.Failure(AuthError.Unknown);
            }

            var token = await response.Content.ReadFromJsonAsync<GotrueTokenResponse>();
            if (token is null)
            {
                logger.LogWarning("Access-token refresh returned an empty response from Supabase.");
                return new Result<AuthSession, AuthError>.Failure(AuthError.Unknown);
            }

            return new Result<AuthSession, AuthError>.Success(
                new AuthSession(token.User.Id, token.User.Email, token.AccessToken, token.RefreshToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during access-token refresh.");
            return new Result<AuthSession, AuthError>.Failure(AuthError.Unknown);
        }
    }

    private sealed record GotrueRefreshRequest([property: JsonPropertyName("refresh_token")] string RefreshToken);

    private sealed record GotrueTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("user")] GotrueTokenUser User);

    private sealed record GotrueTokenUser(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("email")] string Email);

    // Goes straight to the GoTrue REST logout endpoint via a plain HttpClient rather than
    // gotrueClient.SetSession + SignOut, for the same reason RefreshSessionAsync does: SetSession
    // mutates the shared singleton IGotrueClient's "current session", so a logout racing another
    // request's refresh (or another concurrent logout) could stomp or sign out the wrong session.
    // The access token in the request is enough on its own to authorize revoking it.
    public async Task SignOutAsync()
    {
        try
        {
            var accessToken = httpContextAccessor.HttpContext?.User
                .FindFirst(AuthClaimTypes.AccessToken)?.Value;
            if (accessToken is null)
            {
                return;
            }

            var client = httpClientFactory.CreateClient(GotrueHttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "logout?scope=local")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) }
            };
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gotrue sign-out rejected by Supabase with status {StatusCode}.", response.StatusCode);
            }
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
