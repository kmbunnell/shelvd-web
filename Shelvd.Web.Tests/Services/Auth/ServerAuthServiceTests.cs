using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shelvd.Web.Common;
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

    [Fact]
    public async Task SignInAsync_ReturnsSuccessWithSession_WhenCredentialsAreValid()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        gotrueClient
            .Setup(c => c.SignInWithPassword("user@example.com", "password"))
            .ReturnsAsync(CreateSession());
        var sut = new ServerAuthService(gotrueClient.Object, CreateHttpContextAccessor().Object, _logger);

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
        var sut = new ServerAuthService(gotrueClient.Object, CreateHttpContextAccessor().Object, _logger);

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
        var sut = new ServerAuthService(gotrueClient.Object, CreateHttpContextAccessor().Object, _logger);

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
        var sut = new ServerAuthService(gotrueClient.Object, CreateHttpContextAccessor().Object, _logger);

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
        var sut = new ServerAuthService(gotrueClient.Object, CreateHttpContextAccessor().Object, _logger);

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
        var sut = new ServerAuthService(gotrueClient.Object, CreateHttpContextAccessor().Object, _logger);

        var result = await sut.SignUpAsync("new@example.com", "password");

        var failure = Assert.IsType<Result<AuthSession?, AuthError>.Failure>(result);
        Assert.Equal(expected, failure.Error);
    }

    [Fact]
    public async Task SignOutAsync_CallsGotrueSignOut()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        var sut = new ServerAuthService(gotrueClient.Object, CreateHttpContextAccessor().Object, _logger);

        await sut.SignOutAsync();

        gotrueClient.Verify(c => c.SignOut(Constants.SignOutScope.Local), Times.Once);
    }

    [Fact]
    public async Task SignOutAsync_RestoresSessionBeforeSigningOut_WhenTokensArePresentOnUser()
    {
        var claims = new[]
        {
            new Claim(AuthClaimTypes.AccessToken, "access-token"),
            new Claim(AuthClaimTypes.RefreshToken, "refresh-token")
        };
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) };
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        var sut = new ServerAuthService(gotrueClient.Object, CreateHttpContextAccessor(httpContext).Object, _logger);

        await sut.SignOutAsync();

        gotrueClient.Verify(c => c.SetSession("access-token", "refresh-token", false), Times.Once);
        gotrueClient.Verify(c => c.SignOut(Constants.SignOutScope.Local), Times.Once);
    }

    [Fact]
    public async Task SignOutAsync_SkipsSessionRestore_WhenNoTokensOnUser()
    {
        var gotrueClient = new Mock<IGotrueClient<User, Session>>();
        var sut = new ServerAuthService(gotrueClient.Object, CreateHttpContextAccessor().Object, _logger);

        await sut.SignOutAsync();

        gotrueClient.Verify(c => c.SetSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        gotrueClient.Verify(c => c.SignOut(Constants.SignOutScope.Local), Times.Once);
    }
}
