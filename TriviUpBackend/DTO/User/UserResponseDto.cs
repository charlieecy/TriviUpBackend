using System.Text.Json.Serialization;

namespace TriviUpBackend.DTO.User;

/// <summary>
/// DTO de respuesta para información de usuario (usado en endpoints admin).
/// </summary>
public record UserResponseDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("isBanned")] bool IsBanned
);