# Shelvd Web

Blazor Web App companion to the Shelvd KMP mobile app. Lets users manage their book collection via a browser. Portfolio project.

## Solution structure

```
Shelvd.Web/           # ASP.NET Core host — prerendering, server-side endpoints, hides API keys
Shelvd.Web.Client/    # Blazor WASM client — runs in browser, talks to Supabase directly
Shelvd.Web.Tests/     # xUnit + bUnit test project
```

## Tech stack

- .NET 10 Blazor Web App (server + WASM)
- Supabase (auth, database, realtime) — schema owned by the KMP mobile app
- bUnit for component testing

## Build & run

```bash
dotnet build                          # build all projects
dotnet test                           # run tests
dotnet run --project Shelvd.Web       # run locally — https://localhost
```

## Configuration

Copy the template to create your local config file, then fill in real values. The live file is gitignored — never commit it:

```bash
cp Shelvd.Web.Client/wwwroot/appsettings.Template.json Shelvd.Web.Client/wwwroot/appsettings.json
```

```json
{
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "AnonKey": "your-anon-key"
  }
}
```

The anon key is safe to ship to the browser — Supabase RLS protects data.
Server-side secrets (Google Books API key, Supabase service role key) go in `Shelvd.Web/appsettings.json` or user secrets and are never sent to the client.

## Coding conventions

- File-scoped namespaces (`namespace Foo;`)
- Private fields: `_camelCase`
- `var` preferred when type is apparent
- 4-space indent for C#/Razor, 2-space for JSON/YAML/XML
- `TreatWarningsAsErrors` is on — fix warnings, don't suppress them
