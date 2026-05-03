using System.Text.Json.Serialization;

namespace TriviUpBackend.DTO.User;

/// <summary>
/// DTO para actualizar el perfil del usuario autenticado.
/// Todos los campos son opcionales - solo se actualizan los que se proveen.
/// </summary>
public record UpdateUserDto
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }
}