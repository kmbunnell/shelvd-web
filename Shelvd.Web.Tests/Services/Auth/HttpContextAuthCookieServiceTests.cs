using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shelvd.Web.Services.Auth;

namespace Shelvd.Web.Tests.Services.Auth;

public class HttpContextAuthCookieServiceTests
{
    private static (HttpContextAuthCookieService Sut, Mock<IAuthenticationService> AuthenticationService, DefaultHttpContext HttpContext) CreateSut()
    {
        var authenticationService = new Mock<IAuthenticationService>();
        var services = new ServiceCollection();
        services.AddSingleton(authenticationService.Object);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        return (new HttpContextAuthCookieService(accessor.Object), authenticationService, httpContext);
    }

    [Fact]
    public async Task SignInAsync_SignsInClaimsPrincipal_WithUserIdAndEmailClaims()
    {
        var (sut, authenticationService, httpContext) = CreateSut();
        var session = new AuthSession("user-1", "user@example.com", "access-token", "refresh-token");

        await sut.SignInAsync(session);

        authenticationService.Verify(a => a.SignInAsync(
            httpContext,
            CookieAuthenticationDefaults.AuthenticationScheme,
            It.Is<ClaimsPrincipal>(p =>
                p.FindFirst(ClaimTypes.NameIdentifier)!.Value == "user-1" &&
                p.FindFirst(ClaimTypes.Email)!.Value == "user@example.com" &&
                p.FindFirst(AuthClaimTypes.AccessToken)!.Value == "access-token" &&
                p.FindFirst(AuthClaimTypes.RefreshToken)!.Value == "refresh-token"),
            It.IsAny<AuthenticationProperties?>()), Times.Once);
    }

    [Fact]
    public async Task SignOutAsync_SignsOutCookieScheme()
    {
        var (sut, authenticationService, httpContext) = CreateSut();

        await sut.SignOutAsync();

        authenticationService.Verify(a => a.SignOutAsync(
            httpContext,
            CookieAuthenticationDefaults.AuthenticationScheme,
            It.IsAny<AuthenticationProperties?>()), Times.Once);
    }
}
