using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shelvd.Web.Client.Services.Auth;
using Shelvd.Web.Services.Auth;

namespace Shelvd.Web.Tests.Services.Auth;

public class PersistingAuthenticationStateProviderTests : BunitContext
{
    private static ClaimsPrincipal CreateAuthenticatedUser(
        string userId = "user-1",
        string email = "user@example.com",
        string accessToken = "access-token",
        string refreshToken = "refresh-token")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(AuthClaimTypes.AccessToken, accessToken),
            new Claim(AuthClaimTypes.RefreshToken, refreshToken)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsAuthenticatedUser_WhenHttpContextUserIsAuthenticated()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor
            .Setup(a => a.HttpContext)
            .Returns(new DefaultHttpContext { User = CreateAuthenticatedUser() });
        var persistentState = AddBunitPersistentComponentState();
        var sut = new PersistingAuthenticationStateProvider(httpContextAccessor.Object, Services.GetRequiredService<PersistentComponentState>());

        var state = await sut.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity!.IsAuthenticated);
        Assert.Equal("user-1", state.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsAnonymousUser_WhenNoHttpContextUser()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        AddBunitPersistentComponentState();
        var sut = new PersistingAuthenticationStateProvider(httpContextAccessor.Object, Services.GetRequiredService<PersistentComponentState>());

        var state = await sut.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity!.IsAuthenticated);
    }

    [Fact]
    public void OnPersisting_PersistsUserIdAndEmail_WhenUserIsAuthenticated()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor
            .Setup(a => a.HttpContext)
            .Returns(new DefaultHttpContext { User = CreateAuthenticatedUser() });
        var persistentState = AddBunitPersistentComponentState();
        _ = new PersistingAuthenticationStateProvider(httpContextAccessor.Object, Services.GetRequiredService<PersistentComponentState>());

        persistentState.TriggerOnPersisting();

        Assert.True(persistentState.TryTake<PersistedAuthState>("auth", out var persisted));
        Assert.Equal(new PersistedAuthState("user-1", "user@example.com"), persisted);
    }

    [Fact]
    public void OnPersisting_PersistsNothing_WhenUserIsNotAuthenticated()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var persistentState = AddBunitPersistentComponentState();
        _ = new PersistingAuthenticationStateProvider(httpContextAccessor.Object, Services.GetRequiredService<PersistentComponentState>());

        persistentState.TriggerOnPersisting();

        Assert.False(persistentState.TryTake<PersistedAuthState>("auth", out _));
    }
}
