using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;

namespace Shelvd.Web.Client.Services;

public sealed class SupabaseClientWrapper(Supabase.Client inner) : ISupabaseClient
{
    public Task InitializeAsync() => inner.InitializeAsync();
    public IGotrueClient<User, Session> Auth => inner.Auth;
    public IPostgrestTable<T> From<T>() where T : BaseModel, new() => inner.From<T>();
}
