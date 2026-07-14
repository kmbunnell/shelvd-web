# Shelvd Web

A Blazor Web App companion to [Shelvd](https://github.com/kmbunnell/books-kmp), a Kotlin Multiplatform mobile app for tracking your book collection. Shelvd Web lets users manage their library from a browser, sharing the same Supabase backend as the mobile app.

Portfolio project — built to demonstrate a production-shaped .NET/Blazor stack (server prerendering + WASM, Supabase auth/data, CI, TDD) after five years primarily in Android/KMP.

## Status

Early build. Auth (Supabase login/logout, protected routes) is in place; book-collection features are next. See [`docs/roadmap.md`](docs/roadmap.md) for the full build order.

## Tech stack

- .NET 10 Blazor Web App (server + WASM)
- Supabase (auth, database, realtime) — schema owned by the [KMP mobile app](https://github.com/kmbunnell/books-kmp)
- xUnit + bUnit for testing
- GitHub Actions CI (build, format check, test on every PR)

## Getting started

```bash
cp Shelvd.Web/appsettings.Template.json Shelvd.Web/appsettings.json
# fill in Supabase URL + anon key

dotnet build
dotnet test
dotnet run --project Shelvd.Web   # http://localhost:5100
```

Full architecture notes, coding conventions, and configuration details live in [`CLAUDE.md`](CLAUDE.md).

## Related

- [books-kmp](https://github.com/kmbunnell/books-kmp) — the KMP mobile app this project is a companion to; owns the database schema
