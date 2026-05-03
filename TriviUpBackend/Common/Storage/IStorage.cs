using CSharpFunctionalExtensions;
using TriviUpBackend.Errors;

namespace TriviUpBackend.Common.Storage;

/// <summary>
/// Interfaz para el servicio de almacenamiento de archivos.
/// </summary>
public interface IStorage
{
    /// <summary>
    /// Guarda un archivo en una carpeta específica dentro del almacenamiento.
    /// </summary>
    /// <param name="file">El archivo recibido.</param>
    /// <param name="folder">Carpeta destino.</param>
    /// <returns>Resultado con la ruta relativa del archivo guardado o error.</returns>
    Task<Result<string, StorageError>> SaveFileAsync(IFormFile file, string folder);

    /// <summary>
    /// Elimina un archivo del almacenamiento.
    /// </summary>
    /// <param name="filename">Nombre o ruta del archivo.</param>
    /// <returns>Resultado con éxito o error.</returns>
    Task<Result<bool, StorageError>> DeleteFileAsync(string filename);

    /// <summary>
    /// Comprueba si un archivo existe en el almacenamiento.
    /// </summary>
    bool FileExists(string filename);

    /// <summary>
    /// Obtiene la ruta absoluta de un archivo en el sistema de archivos.
    /// </summary>
    string GetFullPath(string filename);
    
    /// <summary>
    /// Obtiene la ruta relativa web para acceder al archivo.
    /// </summary>
    string GetRelativePath(string filename, string folder = "products");
}