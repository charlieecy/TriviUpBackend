using System.ComponentModel.DataAnnotations;

namespace TriviUpBackend.DTO.User;

public record LoginDto
{
    [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
    public string Username { get; init; } = string.Empty;
    
    [Required(ErrorMessage = "La contraseña es obligatoria")]
    public string Password { get; init; } = string.Empty;
};