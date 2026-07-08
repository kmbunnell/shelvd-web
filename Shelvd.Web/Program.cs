using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Shelvd.Web.Client.Pages;
using Shelvd.Web.Components;
using Shelvd.Web.Services.Auth;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpContextAccessor();

// Persist Data Protection keys across restarts so antiforgery tokens issued
// before a dev-server restart still decrypt on the next run instead of failing
// with "The antiforgery token could not be decrypted."
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")))
    .SetApplicationName("Shelvd.Web");

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/login";
        options.ReturnUrlParameter = "returnUrl";
        // Supabase access tokens expire after ~1hr and there is no refresh flow yet;
        // align the cookie lifetime so an expired session forces a clean re-login
        // instead of looking authenticated while every Supabase call 401s.
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();

var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase:Url is not configured in appsettings.json.");
var supabaseAnonKey = builder.Configuration["Supabase:AnonKey"]
    ?? throw new InvalidOperationException("Supabase:AnonKey is not configured in appsettings.json.");

var gotrueHeaders = new Dictionary<string, string> { ["apikey"] = supabaseAnonKey };
builder.Services.AddSingleton<IGotrueClient<User, Session>>(_ => new Client(new ClientOptions
{
    Url = $"{supabaseUrl}/auth/v1",
    AutoRefreshToken = false
})
{
    GetHeaders = () => gotrueHeaders
});
builder.Services.AddScoped<IAuthService, ServerAuthService>();
builder.Services.AddScoped<IAuthCookieService, HttpContextAuthCookieService>();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingAuthenticationStateProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Shelvd.Web.Client._Imports).Assembly);

app.MapPost("/logout", async (IAuthService authService, IAuthCookieService authCookieService) =>
{
    // Clear the local cookie first — it must not stay behind if the Gotrue call below fails.
    await authCookieService.SignOutAsync();
    await authService.SignOutAsync();
    return Results.LocalRedirect("/");
})
.RequireAuthorization()
.AddEndpointFilter(async (context, next) =>
{
    // UseAntiforgery() only validates endpoints carrying IAntiforgeryMetadata, which
    // minimal API endpoints don't get automatically (unlike Razor Components form
    // handlers). Validate explicitly so the <AntiforgeryToken /> in NavMenu.razor is
    // actually enforced, not just rendered.
    var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
    try
    {
        await antiforgery.ValidateRequestAsync(context.HttpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    return await next(context);
});

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program;
