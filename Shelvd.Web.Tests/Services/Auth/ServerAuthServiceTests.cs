using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shelvd.Web.Shared.Common;
using Shelvd.Web.Services.Auth;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;

namespace Shelvd.Web.Tests.Services.Auth;

public class ServerAuthServiceTests
{
    private static readonly NullLogger<ServerAuthService> _logger = NullLogger<ServerAuthService>.Instance;

    private static Session CreateSession(string userId = "user-1", string email = "user@example.com") => new()
    {
        AccessToken = "access-token",
        RefreshToken = "refresh-token",
        User = new User { Id = userId, Email = email }
    };

    private static Mock<IHttpContextAccessor> CreateHttpContextAccessor(HttpContext? httpContext = null)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return accessor;
    }

    private static Mock<IHttpClientFactory> CreateHttpClientFactory(HttpStatusCode statusCode, object? content)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = content is null ? null : JsonContent.Create(content)
            });

        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(ServerAuthService.GotrueHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/auth/v1/") });
        return factory;
    }

    private static ServerAuthService CreateSut(
        Mock<IGotrueClient<User, Session>> gotrueClient,
        Mock<IHttpClientFactory>? httpClientFactory = null,
        HttpContext? httpContext = null) =>
        new(
            gotrueClient.Object,
            (httpClientFactory ?? CreateHttpClientFactory(HttpStatusCode.OK, content: null)).Object,
            CreateHttpContextAccessor(httpContext).Object,
            _logger);

    [Fact]
    public async Task SignInAsync_ReturnsSuccessWithSession_WhenCredentialsAreValid()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        gotrueClient
            .Setup(c => c.SignInWithPassword("user@example.com", "password"))
            .ReturnsAsync(CreateSession());
        var sut = CreateSut(gotrueClient);

        var result = await sut.SignInAsync("user@example.com", "password");

        var success = Assert.IsType<Result<AuthSession?, AuthError>.Success>(result);
        Assert.NotNull(success.Value);
        Assert.Equal("user-1", success.Value.UserId);
        Assert.Equal("user@example.com", success.Value.Email);
        Assert.Equal("access-token", success.Value.AccessToken);
        Assert.Equal("refresh-token", success.Value.RefreshToken);
    }

    [Fact]
    public async Task SignInAsync_ReturnsInvalidCredentials_WhenGotrueThrowsBadLogin()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        gotrueClient
            .Setup(c => c.SignInWithPassword(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new GotrueException("bad login", FailureHint.Reason.UserBadLogin));
        var sut = CreateSut(gotrueClient);

        var result = await sut.SignInAsync("user@example.com", "wrong-password");

        var failure = Assert.IsType<Result<AuthSession?, AuthError>.Failure>(result);
        Assert.Equal(AuthError.InvalidCredentials, failure.Error);
    }

    [Fact]
    public async Task SignInAsync_ReturnsUnknown_WhenUnexpectedExceptionThrown()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        gotrueClient
            .Setup(c => c.SignInWithPassword(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut(gotrueClient);

        var result = await sut.SignInAsync("user@example.com", "password");

        var failure = Assert.IsType<Result<AuthSession?, AuthError>.Failure>(result);
        Assert.Equal(AuthError.Unknown, failure.Error);
    }

    [Fact]
    public async Task SignUpAsync_ReturnsSuccess_WhenSignUpSucceeds()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        gotrueClient
            .Setup(c => c.SignUp("new@example.com", "password", null))
            .ReturnsAsync(CreateSession(email: "new@example.com"));
        var sut = CreateSut(gotrueClient);

        var result = await sut.SignUpAsync("new@example.com", "password");

        Assert.IsType<Result<AuthSession?, AuthError>.Success>(result);
    }

    [Fact]
    public async Task SignUpAsync_ReturnsSuccessWithNoSession_WhenEmailConfirmationRequired()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        gotrueClient
            .Setup(c => c.SignUp("new@example.com", "password", null))
            .ReturnsAsync((Session?)null);
        var sut = CreateSut(gotrueClient);

        var result = await sut.SignUpAsync("new@example.com", "password");

        var success = Assert.IsType<Result<AuthSession?, AuthError>.Success>(result);
        Assert.Null(success.Value);
    }

    [Theory]
    [InlineData(FailureHint.Reason.UserAlreadyRegistered, AuthError.EmailAlreadyInUse)]
    [InlineData(FailureHint.Reason.UserBadPassword, AuthError.WeakPassword)]
    [InlineData(FailureHint.Reason.UserBadEmailAddress, AuthError.InvalidEmail)]
    public async Task SignUpAsync_MapsKnownGotrueErrors(FailureHint.Reason reason, AuthError expected)
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        gotrueClient
            .Setup(c => c.SignUp(It.IsAny<string>(), It.IsAny<string>(), null))
            .ThrowsAsync(new GotrueException("bad signup", reason));
        var sut = CreateSut(gotrueClient);

        var result = await sut.SignUpAsync("new@example.com", "password");

        var failure = Assert.IsType<Result<AuthSession?, AuthError>.Failure>(result);
        Assert.Equal(expected, failure.Error);
    }

    [Fact]
    public async Task RefreshSessionAsync_ReturnsSuccessWithNewTokens_WhenSupabaseAcceptsTheRefreshToken()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        var httpClientFactory = CreateHttpClientFactory(HttpStatusCode.OK, new
        {
            access_token = "new-access-token",
            refresh_token = "new-refresh-token",
            user = new { id = "user-1", email = "user@example.com" }
        });
        var sut = CreateSut(gotrueClient, httpClientFactory);

        var result = await sut.RefreshSessionAsync("old-access-token", "old-refresh-token");

        var success = Assert.IsType<Result<AuthSession, AuthError>.Success>(result);
        Assert.Equal("user-1", success.Value.UserId);
        Assert.Equal("user@example.com", success.Value.Email);
        Assert.Equal("new-access-token", success.Value.AccessToken);
        Assert.Equal("new-refresh-token", success.Value.RefreshToken);
        gotrueClient.Verify(c => c.SetSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task RefreshSessionAsync_SendsRefreshTokenToTheGotrueTokenEndpoint()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                capturedRequest = request;
                capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    access_token = "new-access-token",
                    refresh_token = "new-refresh-token",
                    user = new { id = "user-1", email = "user@example.com" }
                })
            });
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(ServerAuthService.GotrueHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/auth/v1/") });
        var sut = CreateSut(gotrueClient, factory);

        await sut.RefreshSessionAsync("old-access-token", "old-refresh-token");

        Assert.NotNull(capturedRequest);
        Assert.Equal("/auth/v1/token", capturedRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?grant_type=refresh_token", capturedRequest.RequestUri.Query);
        using var body = JsonDocument.Parse(capturedBody!);
        Assert.Equal("old-refresh-token", body.RootElement.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task RefreshSessionAsync_ReturnsUnknown_WhenSupabaseRejectsTheRefreshToken()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        var httpClientFactory = CreateHttpClientFactory(HttpStatusCode.Unauthorized, content: null);
        var sut = CreateSut(gotrueClient, httpClientFactory);

        var result = await sut.RefreshSessionAsync("old-access-token", "old-refresh-token");

        var failure = Assert.IsType<Result<AuthSession, AuthError>.Failure>(result);
        Assert.Equal(AuthError.Unknown, failure.Error);
    }

    [Fact]
    public async Task RefreshSessionAsync_ReturnsUnknown_WhenHttpCallThrows()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network unreachable"));
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(ServerAuthService.GotrueHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/auth/v1/") });
        var sut = CreateSut(gotrueClient, factory);

        var result = await sut.RefreshSessionAsync("old-access-token", "old-refresh-token");

        var failure = Assert.IsType<Result<AuthSession, AuthError>.Failure>(result);
        Assert.Equal(AuthError.Unknown, failure.Error);
    }

    [Fact]
    public async Task SignOutAsync_PostsToGotrueLogoutEndpoint_WhenAccessTokenPresent()
    {
        var claims = new[] { new Claim(AuthClaimTypes.AccessToken, "access-token") };
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) };
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        HttpRequestMessage? capturedRequest = null;
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(ServerAuthService.GotrueHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/auth/v1/") });
        var sut = CreateSut(gotrueClient, factory, httpContext);

        await sut.SignOutAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://test.supabase.co/auth/v1/logout?scope=local", capturedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("access-token", capturedRequest.Headers.Authorization?.Parameter);
        gotrueClient.Verify(c => c.SetSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        gotrueClient.Verify(c => c.SignOut(It.IsAny<Constants.SignOutScope>()), Times.Never);
    }

    [Fact]
    public async Task SignOutAsync_SkipsHttpCall_WhenNoAccessTokenOnUser()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        var handler = new Mock<HttpMessageHandler>();
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(ServerAuthService.GotrueHttpClientName))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("https://test.supabase.co/auth/v1/") });
        var sut = CreateSut(gotrueClient, factory);

        await sut.SignOutAsync();

        handler.Protected().Verify(
            "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }
}
