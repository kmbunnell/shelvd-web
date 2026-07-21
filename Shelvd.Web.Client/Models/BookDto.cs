using System.Text.Json.Serialization;

namespace Shelvd.Web.Client.Models;

public sealed record BookDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("isbn")] string? Isbn,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("authors")] IReadOnlyList<string> Authors,
    [property: JsonPropertyName("cover_image_url")] string? CoverImageUrl,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt);
