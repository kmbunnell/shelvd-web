using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Shelvd.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase:Url is not configured in appsettings.json.");
var supabaseAnonKey = builder.Configuration["Supabase:AnonKey"]
    ?? throw new InvalidOperationException("Supabase:AnonKey is not configured in appsettings.json.");

var supabaseClient = new Supabase.Client(
    supabaseUrl,
    supabaseAnonKey,
    new Supabase.SupabaseOptions { AutoConnectRealtime = false });

builder.Services.AddSingleton<ISupabaseClient>(new SupabaseClientWrapper(supabaseClient));

var host = builder.Build();

await host.Services.GetRequiredService<ISupabaseClient>().InitializeAsync();

await host.RunAsync();
