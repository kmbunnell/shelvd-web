using System.Text.Json.Serialization;

namespace Shelvd.Web.Client.Models;

public sealed record TagDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("is_default")] bool IsDefault);
