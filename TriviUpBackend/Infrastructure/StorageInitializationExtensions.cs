using Microsoft.Extensions.Logging;
using Path = System.IO.Path;

namespace TriviUpBackend.Infrastructure;

public static class StorageInitializationExtensions
{
    /// <summary>
    /// Inicializa el directorio de almacenamiento de archivos.
    /// Desarrollo: Borra y recrea el directorio.
    /// Producción: Solo crea si no existe.
    /// </summary>
    public static void InitializeStorage(this WebApplication app)
    {
        var storagePath = Path.Combine(app.Environment.WebRootPath, "uploads");
        var storageDirectory = new DirectoryInfo(storagePath);
        
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("StorageInitialization");
        logger.LogInformation("Verificando directorio de almacenamiento: {Path}", storagePath);
        
        try
        {
            if (!storageDirectory.Exists)
            {
                storageDirectory.Create();
                logger.LogInformation("Directorio de almacenamiento creado");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al verificar directorio de almacenamiento");
        }
        
    }
}
