using System.Text.Json.Serialization;

namespace TriviUpBackend.DTO.User;

public record AuthResponseDto(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("user")] UserDto User
);