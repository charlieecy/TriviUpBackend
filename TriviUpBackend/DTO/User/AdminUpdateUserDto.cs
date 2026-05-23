using System.Text.Json.Serialization;

namespace TriviUpBackend.DTO.User;

/// <summary>
/// DTO para que un admin actualice un usuario.
/// Todos los campos son opcionales - solo se actualizan los que se proveen.
/// </summary>
public record AdminUpdateUserDto
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }
}