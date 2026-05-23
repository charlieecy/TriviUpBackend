using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IWebHostEnvironment;

namespace TriviUpBackend.Controllers;

[ApiController]
[Route("[controller]")]
public class StorageController(
    IWebHostEnvironment env,
    ILogger<StorageController> logger
) : ControllerBase
{
    /// <summary>
    /// Sirve archivos de la carpeta uploads.
    /// GET /storage/profile-photos/archivo.png
    /// </summary>
    [HttpGet("{**path}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetFile(string path)
    {
        logger.LogInformation("[StorageController] Intentando servir archivo: {Path}", path);

        var uploadsPath = Path.Combine(env.ContentRootPath, "uploads");
        var fullPath = Path.GetFullPath(Path.Combine(uploadsPath, path));

        // Seguridad: asegurar que el path está dentro de uploads
        if (!fullPath.StartsWith(Path.GetFullPath(uploadsPath)))
        {
            logger.LogWarning("[StorageController] Intento de acceso fuera de uploads: {Path}", path);
            return NotFound();
        }

        if (!System.IO.File.Exists(fullPath))
        {
            logger.LogWarning("[StorageController] Archivo no encontrado: {Path}", fullPath);
            return NotFound(new { message = "Archivo no encontrado" });
        }

        var contentType = GetContentType(fullPath);
        logger.LogInformation("[StorageController] Sirviendo archivo: {Path}, ContentType: {Type}", fullPath, contentType);

        return PhysicalFile(fullPath, contentType);
    }

    private static string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }
}