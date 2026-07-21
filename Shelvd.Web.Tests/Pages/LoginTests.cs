using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shelvd.Web.Components.Pages;
using Shelvd.Web.Services.Auth;
using Shelvd.Web.Shared.Common;

namespace Shelvd.Web.Tests.Pages;

public class LoginTests : BunitContext
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IAuthCookieService> _authCookieService = new();

    public LoginTests()
    {
        Services.AddSingleton(_authService.Object);
        Services.AddSingleton(_authCookieService.Object);
    }

    [Fact]
    public void Submit_CallsSignInAsync_WithEnteredCredentials()
    {
        _authService
            .Setup(s => s.SignInAsync("user@example.com", "password"))
            .ReturnsAsync(new Result<AuthSession?, AuthError>.Success(new AuthSession("user-1", "user@example.com", "token", "refresh")));

        var cut = Render<Login>();
        cut.Find("#email").Change("user@example.com");
        cut.Find("#password").Change("password");
        cut.Find("form").Submit();

        _authService.Verify(s => s.SignInAsync("user@example.com", "password"), Times.Once);
    }

    [Fact]
    public void Submit_RendersErrorMessage_WhenSignInFails()
    {
        _authService
            .Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Result<AuthSession?, AuthError>.Failure(AuthError.InvalidCredentials));

        var cut = Render<Login>();
        cut.Find("#email").Change("user@example.com");
        cut.Find("#password").Change("wrong-password");
        cut.Find("form").Submit();

        Assert.Contains(AuthErrorMessages.For(AuthError.InvalidCredentials), cut.Markup);
        _authCookieService.Verify(s => s.SignInAsync(It.IsAny<AuthSession>()), Times.Never);
    }

    [Fact]
    public void Submit_SignsInCookieAndRedirectsToReturnUrl_WhenSignInSucceeds()
    {
        var session = new AuthSession("user-1", "user@example.com", "token", "refresh");
        _authService
            .Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Result<AuthSession?, AuthError>.Success(session));

        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("returnUrl", "/books"));

        var cut = Render<Login>();
        cut.Find("#email").Change("user@example.com");
        cut.Find("#password").Change("password");
        cut.Find("form").Submit();

        _authCookieService.Verify(s => s.SignInAsync(session), Times.Once);
        Assert.EndsWith("/books", navigation.Uri);
    }
}
