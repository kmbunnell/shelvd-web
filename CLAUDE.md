# Shelvd Web

Blazor Web App companion to the Shelvd KMP mobile app. Lets users manage their book collection via a browser. Portfolio project.

## Solution structure

```
Shelvd.Web/           # ASP.NET Core host — prerendering, server-side endpoints, hides API keys
Shelvd.Web.Client/    # Blazor WASM client — runs in browser, UI shell with no direct Supabase access
Shelvd.Web.Tests/     # xUnit + bUnit test project
```

`Shelvd.Web` has a project reference to `Shelvd.Web.Client` (never the other way — the client runs standalone in the browser). This means a plain-data type that crosses the server/client boundary — e.g. `PersistedAuthState` in `Shelvd.Web.Client/Services/Auth` — lives in the client project, since the server can see it and the client can't see the server. This is expected: it's a wire contract owned by whichever side actually consumes it (the client, in this case), not a sign that server logic belongs in the client. Auth types that only `Shelvd.Web` consumes (`IAuthService`, `AuthSession`, `AuthError`, etc.) live in `Shelvd.Web` itself.

If a type genuinely has no natural owner (neither side is clearly the consumer), or shared *logic* (not just data shapes) is needed on both sides, that's the point to introduce a `Shelvd.Web.Shared` project rather than keep growing the client project.

## Tech stack

- .NET 10 Blazor Web App (server + WASM)
- Supabase (auth, database, realtime) — schema owned by the KMP mobile app
- bUnit for component testing

## Build & run

```bash
dotnet build                          # build all projects
dotnet test                           # run tests
dotnet run --project Shelvd.Web       # run locally — http://localhost:5100
```

Local dev runs over plain HTTP (`http` launch profile) — the ASP.NET Core dev HTTPS cert isn't trusted on this machine, and since this is a portfolio/practice project, that's fine for now. An `https` launch profile exists (`dotnet run --project Shelvd.Web --launch-profile https`) if HTTPS is ever needed locally; run `dotnet dev-certs https --trust` first. Production still enforces HTTPS via `UseHsts()`/`UseHttpsRedirection()` in `Program.cs`, so this only affects local dev.

## Configuration

Copy the template to create your local config file, then fill in real values. The live file is gitignored — never commit it:

```bash
cp Shelvd.Web/appsettings.Template.json Shelvd.Web/appsettings.json
```

```json
{
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "AnonKey": "your-anon-key"
  }
}
```

Supabase config only lives server-side in `Shelvd.Web` — the WASM client has no direct Supabase access and needs no config of its own.
Server-side secrets (Google Books API key, Supabase service role key) go in `Shelvd.Web/appsettings.json` or user secrets and are never sent to the client. Prefer user secrets (`dotnet user-secrets`) for anything more sensitive than the anon key/URL — `appsettings.json` is a plain local file with no extra protection beyond being gitignored.

## Coding conventions

- File-scoped namespaces (`namespace Foo;`)
- Private fields: `_camelCase`
- `var` preferred when type is apparent
- 4-space indent for C#/Razor, 2-space for JSON/YAML/XML
- `TreatWarningsAsErrors` is on — fix warnings, don't suppress them
- Build query strings with `QueryHelpers.AddQueryString` (`Microsoft.AspNetCore.WebUtilities`, free via the `Microsoft.NET.Sdk.Web` SDK) rather than hand-concatenated literals
- Before adding a new method, component, or type, check for an existing one that already does this or could be extended — search first, write second
- `CancellationToken` handling differs by side, since the two have different lifetimes:
  - `Shelvd.Web.Client` (WASM) methods take `CancellationToken cancellationToken = default` and thread it through every downstream async call (`HttpClient` calls, `ReadFromJsonAsync`, etc.), catching and rethrowing `OperationCanceledException` before other exception handlers — a call's lifetime can outlive the component/screen that started it (e.g. user navigates away), so it needs to be cancellable independently.
  - `Shelvd.Web` (server) methods don't take an explicit `CancellationToken` — they're already scoped to the incoming HTTP request, which has its own cancellation via `HttpContext.RequestAborted`.
