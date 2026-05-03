using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TriviUpBackend.DTO.User;

/// <summary>
/// DTO para actualizar la foto de perfil de un usuario.
/// </summary>
public record UpdateProfilePhotoDto(
    [property: JsonPropertyName("file")] IFormFile File
);
