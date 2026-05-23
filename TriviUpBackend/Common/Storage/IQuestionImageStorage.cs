using CSharpFunctionalExtensions;

namespace TriviUpBackend.Common.Storage;

/// <summary>
/// Interfaz para el servicio de almacenamiento de imágenes de preguntas.
/// </summary>
public interface IQuestionImageStorage
{
    /// <summary>
    /// Guarda una imagen para una pregunta específica.
    /// </summary>
    /// <param name="questionId">ID de la pregunta.</param>
    /// <param name="file">Archivo de imagen.</param>
    /// <returns>Resultado con la ruta relativa de la imagen guardada o error.</returns>
    Task<Result<string, QuestionImageStorageError>> SaveQuestionImageAsync(long questionId, IFormFile file);

    /// <summary>
    /// Elimina la imagen de una pregunta.
    /// </summary>
    /// <param name="imageUrl">Ruta relativa de la imagen a eliminar.</param>
    /// <returns>Resultado con éxito o error.</returns>
    Task<Result<bool, QuestionImageStorageError>> DeleteQuestionImageAsync(string imageUrl);

    /// <summary>
    /// Obtiene la URL completa de una imagen de pregunta.
    /// </summary>
    /// <param name="relativePath">Ruta relativa de la imagen.</param>
    /// <returns>URL completa para acceder a la imagen.</returns>
    string GetFullUrl(string relativePath);
}

/// <summary>
/// Errores específicos del servicio de almacenamiento de imágenes de preguntas.
/// </summary>
public record QuestionImageStorageError(string Error);