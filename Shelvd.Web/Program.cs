using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Shelvd.Web.Client.Models;
using Shelvd.Web.Components;
using Shelvd.Web.Middleware;
using Shelvd.Web.Services.Auth;
using Shelvd.Web.Services.Books;
using Shelvd.Web.Services.Tags;
using Shelvd.Web.Shared.Common;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

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
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Local dev runs over plain HTTP (see CLAUDE.md) — Always would silently drop the
        // cookie there since browsers won't store a Secure cookie from an insecure origin.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        // AccessTokenRefreshMiddleware silently refreshes the Supabase access token
        // and reissues the cookie with a fresh absolute expiry (see
        // AccessTokenRefreshService), so this is the max session lifetime between
        // refreshes rather than a hard cutoff tied to the original login.
        options.ExpireTimeSpan = AuthCookieOptions.ExpireTimeSpan;
        options.SlidingExpiration = false;
        // Without this, an unauthenticated /api/* request gets the same 302-to-/login
        // response as an unauthenticated page navigation. HttpClient follows that redirect
        // automatically, so callers like BooksApiClient would receive a 200 OK with the
        // login page's HTML instead of a 401 they can detect.
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
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
builder.Services.AddHttpClient(ServerAuthService.GotrueHttpClientName, client =>
{
    client.BaseAddress = new Uri($"{supabaseUrl}/auth/v1/");
    client.DefaultRequestHeaders.Add("apikey", supabaseAnonKey);
});
builder.Services.AddHttpClient(BooksService.SupabaseRestHttpClientName, client =>
{
    client.BaseAddress = new Uri($"{supabaseUrl}/rest/v1/");
    client.DefaultRequestHeaders.Add("apikey", supabaseAnonKey);
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAuthService, ServerAuthService>();
builder.Services.AddScoped<IAuthCookieService, HttpContextAuthCookieService>();
builder.Services.AddScoped<IAccessTokenRefreshService, AccessTokenRefreshService>();
builder.Services.AddScoped<IBooksService, BooksService>();
builder.Services.AddScoped<ITagsService, TagsService>();

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
// Static assets (css/js/images/fonts, _framework/*) aren't auth-gated and have no
// use for a refreshed session cookie, so skip the refresh check for them — avoids a
// JWT parse on every asset request. An explicit allow-list (rather than "any path with
// an extension") avoids accidentally skipping the refresh check for real endpoints that
// happen to have a dotted segment, e.g. a future /api/books.json-style route.
app.UseWhen(
    ctx => !IsStaticAssetRequest(ctx.Request.Path),
    branch => branch.UseMiddleware<AccessTokenRefreshMiddleware>());
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Shelvd.Web.Client._Imports).Assembly);

app.MapPost("/logout", async (
    HttpContext httpContext,
    IAuthService authService,
    IAuthCookieService authCookieService,
    IAccessTokenRefreshService refreshService) =>
{
    // A refresh that lands on this same request rotates the refresh token before this handler
    // runs, so the claim below may already be the new token while the refresh cache entry is
    // still keyed by the pre-refresh one (see AccessTokenRefreshService). Clear both.
    var currentRefreshToken = httpContext.User.FindFirst(AuthClaimTypes.RefreshToken)?.Value;
    var preRefreshToken = httpContext.Items["PreRefreshRefreshToken"] as string;
    if (currentRefreshToken is not null)
    {
        refreshService.ClearCachedSession(currentRefreshToken);
    }
    if (preRefreshToken is not null && preRefreshToken != currentRefreshToken)
    {
        refreshService.ClearCachedSession(preRefreshToken);
    }

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

app.MapGet("/api/books", async (HttpContext httpContext, IBooksService booksService) =>
{
    var accessToken = httpContext.User.FindFirst(AuthClaimTypes.AccessToken)?.Value;
    if (accessToken is null)
    {
        return Results.Unauthorized();
    }

    var result = await booksService.GetBooksAsync(accessToken);
    return result switch
    {
        Result<IReadOnlyList<BookDto>, BooksError>.Success success => Results.Ok(success.Value),
        Result<IReadOnlyList<BookDto>, BooksError>.Failure failure => failure.Error switch
        {
            BooksError.Unauthenticated => Results.Unauthorized(),
            _ => Results.StatusCode(StatusCodes.Status502BadGateway)
        },
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
})
.RequireAuthorization();

app.MapGet("/api/books/{id:guid}", async (HttpContext httpContext, IBooksService booksService, Guid id) =>
{
    var accessToken = httpContext.User.FindFirst(AuthClaimTypes.AccessToken)?.Value;
    if (accessToken is null)
    {
        return Results.Unauthorized();
    }

    var result = await booksService.GetBookByIdAsync(accessToken, id);
    return result switch
    {
        Result<BookDto?, BooksError>.Success { Value: null } => Results.NotFound(),
        Result<BookDto?, BooksError>.Success success => Results.Ok(success.Value),
        Result<BookDto?, BooksError>.Failure failure => failure.Error switch
        {
            BooksError.Unauthenticated => Results.Unauthorized(),
            _ => Results.StatusCode(StatusCodes.Status502BadGateway)
        },
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
})
.RequireAuthorization();

app.MapGet("/api/tags", async (HttpContext httpContext, ITagsService tagsService) =>
{
    var accessToken = httpContext.User.FindFirst(AuthClaimTypes.AccessToken)?.Value;
    if (accessToken is null)
    {
        return Results.Unauthorized();
    }

    var result = await tagsService.GetTagsAsync(accessToken);
    return result switch
    {
        Result<IReadOnlyList<TagDto>, TagsError>.Success success => Results.Ok(success.Value),
        Result<IReadOnlyList<TagDto>, TagsError>.Failure failure => failure.Error switch
        {
            TagsError.Unauthenticated => Results.Unauthorized(),
            _ => Results.StatusCode(StatusCodes.Status502BadGateway)
        },
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
})
.RequireAuthorization();

app.Run();

static bool IsStaticAssetRequest(PathString path)
{
    var value = path.Value;
    if (string.IsNullOrEmpty(value))
    {
        return false;
    }

    if (value.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "/favicon.ico", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return _staticAssetExtensions.Contains(Path.GetExtension(value));
}

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program
{
    private static readonly HashSet<string> _staticAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".js", ".map", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico",
        ".woff", ".woff2", ".ttf", ".eot", ".wasm", ".dll"
    };
}
