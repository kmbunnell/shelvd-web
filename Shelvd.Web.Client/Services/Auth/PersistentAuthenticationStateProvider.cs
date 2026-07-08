using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Shelvd.Web.Client.Services.Auth;

public sealed class PersistentAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly Task<AuthenticationState> _authenticationStateTask;

    public PersistentAuthenticationStateProvider(PersistentComponentState persistentState)
    {
        if (persistentState.TryTakeFromJson<PersistedAuthState>("auth", out var persisted) && persisted is not null)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, persisted.UserId),
                new Claim(ClaimTypes.Email, persisted.Email)
            };
            var identity = new ClaimsIdentity(claims, "PersistedAuth");
            _authenticationStateTask = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
        else
        {
            _authenticationStateTask = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
}
