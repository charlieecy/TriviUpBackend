using CSharpFunctionalExtensions;

namespace TriviUpBackend.Common.Storage;

/// <summary>
/// Interfaz para el servicio de almacenamiento de fotos de perfil.
/// </summary>
public interface IProfilePhotoStorage
{
    /// <summary>
    /// Guarda una foto de perfil para un usuario específico.
    /// </summary>
    /// <param name="userId">ID del usuario.</param>
    /// <param name="file">Archivo de imagen.</param>
    /// <returns>Resultado con la ruta relativa del archivo guardado o error.</returns>
    Task<Result<string, ProfilePhotoStorageError>> SaveProfilePhotoAsync(long userId, IFormFile file);

    /// <summary>
    /// Elimina la foto de perfil de un usuario.
    /// </summary>
    /// <param name="photoUrl">Ruta relativa de la foto a eliminar.</param>
    /// <returns>Resultado con éxito o error.</returns>
    Task<Result<bool, ProfilePhotoStorageError>> DeleteProfilePhotoAsync(string photoUrl);

    /// <summary>
    /// Obtiene la URL completa de una foto de perfil.
    /// </summary>
    /// <param name="relativePath">Ruta relativa de la foto.</param>
    /// <returns>URL completa para acceder a la foto.</returns>
    string GetFullUrl(string relativePath);
}

/// <summary>
/// Errores específicos del servicio de almacenamiento de fotos de perfil.
/// </summary>
public record ProfilePhotoStorageError(string Error);
