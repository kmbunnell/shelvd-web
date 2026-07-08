using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shelvd.Web.Client.Services.Auth;

namespace Shelvd.Web.Tests.Services.Auth;

public class PersistentAuthenticationStateProviderTests : BunitContext
{
    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsAuthenticatedUser_WhenAuthStateWasPersisted()
    {
        var persistentState = AddBunitPersistentComponentState();
        persistentState.Persist("auth", new PersistedAuthState("user-1", "user@example.com"));
        var sut = new PersistentAuthenticationStateProvider(Services.GetRequiredService<PersistentComponentState>());

        var state = await sut.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity!.IsAuthenticated);
        Assert.Equal("user-1", state.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        Assert.Equal("user@example.com", state.User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsAnonymousUser_WhenNothingWasPersisted()
    {
        AddBunitPersistentComponentState();
        var sut = new PersistentAuthenticationStateProvider(Services.GetRequiredService<PersistentComponentState>());

        var state = await sut.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity!.IsAuthenticated);
    }
}
