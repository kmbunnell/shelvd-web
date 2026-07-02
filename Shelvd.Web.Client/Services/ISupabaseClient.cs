using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;

namespace Shelvd.Web.Client.Services;

public interface ISupabaseClient
{
    Task InitializeAsync();
    IGotrueClient<User, Session> Auth { get; }
    IPostgrestTable<T> From<T>() where T : BaseModel, new();
}
