using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Shelvd.Web.Client.Services.Auth;

namespace Shelvd.Web.Services.Auth;

public sealed class PersistingAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PersistentComponentState _persistentState;
    private readonly PersistingComponentStateSubscription _subscription;

    public PersistingAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor, PersistentComponentState persistentState)
    {
        _httpContextAccessor = httpContextAccessor;
        _persistentState = persistentState;
        _subscription = _persistentState.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(user));
    }

    private Task OnPersistingAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = user.FindFirst(ClaimTypes.Email)?.Value;

        if (userId is null || email is null)
        {
            return Task.CompletedTask;
        }

        _persistentState.PersistAsJson("auth", new PersistedAuthState(userId, email));
        return Task.CompletedTask;
    }

    public void Dispose() => _subscription.Dispose();
}
