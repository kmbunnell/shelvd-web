using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shelvd.Web.Common;
using Shelvd.Web.Components.Pages;
using Shelvd.Web.Services.Auth;

namespace Shelvd.Web.Tests.Pages;

public class RegisterTests : BunitContext
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IAuthCookieService> _authCookieService = new();

    public RegisterTests()
    {
        Services.AddSingleton(_authService.Object);
        Services.AddSingleton(_authCookieService.Object);
    }

    [Fact]
    public void Submit_CallsSignUpAsync_WithEnteredCredentials()
    {
        _authService
            .Setup(s => s.SignUpAsync("new@example.com", "password"))
            .ReturnsAsync(new Result<AuthSession?, AuthError>.Success(null));

        var cut = Render<Register>();
        cut.Find("#email").Change("new@example.com");
        cut.Find("#password").Change("password");
        cut.Find("form").Submit();

        _authService.Verify(s => s.SignUpAsync("new@example.com", "password"), Times.Once);
    }

    [Fact]
    public void Submit_ShowsCheckYourEmailMessage_WhenSignUpSucceeds()
    {
        _authService
            .Setup(s => s.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Result<AuthSession?, AuthError>.Success(null));

        var cut = Render<Register>();
        cut.Find("#email").Change("new@example.com");
        cut.Find("#password").Change("password");
        cut.Find("form").Submit();

        Assert.Contains("check your email", cut.Markup, StringComparison.OrdinalIgnoreCase);
        _authCookieService.Verify(s => s.SignInAsync(It.IsAny<AuthSession>()), Times.Never);
    }

    [Fact]
    public void Submit_SignsInCookieAndRedirectsHome_WhenSignUpReturnsActiveSession()
    {
        var session = new AuthSession("user-1", "new@example.com", "token", "refresh");
        _authService
            .Setup(s => s.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Result<AuthSession?, AuthError>.Success(session));

        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<Register>();
        cut.Find("#email").Change("new@example.com");
        cut.Find("#password").Change("password");
        cut.Find("form").Submit();

        _authCookieService.Verify(s => s.SignInAsync(session), Times.Once);
        Assert.EndsWith("/", navigation.Uri);
    }

    [Theory]
    [InlineData(AuthError.EmailAlreadyInUse)]
    [InlineData(AuthError.WeakPassword)]
    [InlineData(AuthError.InvalidEmail)]
    public void Submit_RendersDistinctErrorMessage_WhenSignUpFails(AuthError error)
    {
        _authService
            .Setup(s => s.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Result<AuthSession?, AuthError>.Failure(error));

        var cut = Render<Register>();
        cut.Find("#email").Change("new@example.com");
        cut.Find("#password").Change("password");
        cut.Find("form").Submit();

        Assert.Contains(AuthErrorMessages.For(error), cut.Markup);
    }
}
