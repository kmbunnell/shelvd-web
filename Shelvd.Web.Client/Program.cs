using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Shelvd.Web.Client.Services.Auth;
using Shelvd.Web.Client.Services.Books;
using Shelvd.Web.Client.Services.BookTags;
using Shelvd.Web.Client.Services.Tags;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IBooksApiClient, BooksApiClient>();
builder.Services.AddScoped<ITagsApiClient, TagsApiClient>();
builder.Services.AddScoped<ITagsCache, TagsCache>();
builder.Services.AddScoped<IBookTagsApiClient, BookTagsApiClient>();

var host = builder.Build();

await host.RunAsync();
