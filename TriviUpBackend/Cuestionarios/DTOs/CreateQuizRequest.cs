using System.ComponentModel.DataAnnotations;

namespace TriviUpBackend.Cuestionarios.DTOs;

public record CreateQuizRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [MinLength(1, ErrorMessage = "El nombre no puede estar vacío")]
    [MaxLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "Las preguntas son obligatorias")]
    [MinLength(1, ErrorMessage = "Debe haber al menos una pregunta")]
    public List<CreatePreguntaRequest> Preguntas { get; init; } = new();
}

public record CreatePreguntaRequest
{
    [Required(ErrorMessage = "El número de pregunta es obligatorio")]
    [Range(1, int.MaxValue, ErrorMessage = "El número de pregunta debe ser mayor a 0")]
    public int NumeroPregunta { get; init; }

    [Required(ErrorMessage = "El enunciado es obligatorio")]
    [MinLength(1, ErrorMessage = "El enunciado no puede estar vacío")]
    [MaxLength(1000, ErrorMessage = "El enunciado no puede exceder 1000 caracteres")]
    public string Enunciado { get; init; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "La URL de imagen no puede exceder 2000 caracteres")]
    public string? ImagenUrl { get; init; }

    [Required(ErrorMessage = "Las respuestas son obligatorias")]
    [MinLength(2, ErrorMessage = "Debe haber al menos 2 respuestas")]
    public List<CreateRespuestaRequest> Respuestas { get; init; } = new();
}

public record CreateRespuestaRequest
{
    [Required(ErrorMessage = "El texto es obligatorio")]
    [MinLength(1, ErrorMessage = "El texto no puede estar vacío")]
    [MaxLength(500, ErrorMessage = "El texto no puede exceder 500 caracteres")]
    public string Texto { get; init; } = string.Empty;

    public bool EsCorrecta { get; init; } = false;
}
