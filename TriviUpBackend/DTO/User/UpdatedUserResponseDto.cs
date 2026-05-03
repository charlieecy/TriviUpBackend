using System.Text.Json.Serialization;
using TriviUpBackend.DTO.User;

namespace TriviUpBackend.DTO.User;

/// <summary>
/// DTO de respuesta para la actualización exitosa del perfil de usuario.
/// </summary>
public record UpdatedUserResponseDto(
    [property: JsonPropertyName("user")] UserDto User,
    [property: JsonPropertyName("message")] string Message
);